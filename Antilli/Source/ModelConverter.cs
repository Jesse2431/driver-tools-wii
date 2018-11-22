﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

using System.Xml;
using System.Xml.Linq;

using DSCript;
using DSCript.Models;
using DSCript.Spooling;

using COLLADA;

namespace Antilli
{
    

    public enum EffectType
    {
        Static,
        Vehicle,
        Character,
    }
    
    public class ModelConverter
    {
        // yes, it's just a fancy VertexBuffer :P
        public struct VerticesHolder
        {
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector2[] TexCoords;

            public bool HasPositions
            {
                get { return Positions != null; }
            }

            public bool HasNormals
            {
                get { return Normals != null; }
            }

            public bool HasTexCoords
            {
                get { return TexCoords != null; }
            }
            
            private static Vector2 ReadVector2(COLLADAFloatArray array, int index)
            {
                return new Vector2() {
                    X = array.Data[index + 0],
                    Y = array.Data[index + 1],
                };
            }

            private static Vector3 ReadVector3(COLLADAFloatArray array, int index)
            {
                // HACK: assume we need to correct the directions!
                return new Vector3() {
                    X = -array.Data[index + 0],
                    Y = array.Data[index + 2],
                    Z = array.Data[index + 1],
                };
            }

            private static void ReadVectorData<T>(COLLADASource source, Func<COLLADAFloatArray, int, T> fnReadData, out T[] data)
            {
                var array = source.Array;
                var accessor = source.Technique.Accessor;

                var stride = accessor.Stride;

                data = new T[accessor.Count];

                var vIdx = 0;

                for (int v = 0; v < array.Count; v += stride)
                {
                    data[vIdx++] = fnReadData(array, v);
                }
            }

            public VertexData CreateVertex(VertexBuffer vertexBuffer, PolyMesh.Primitive prim)
            {
                var vertex = vertexBuffer.CreateVertex();

                if (HasPositions)
                    vertex.SetData(VertexUsageType.Position, 0, Positions[prim.Position]);
                if (HasNormals)
                    vertex.SetData(VertexUsageType.Normal, 0, Normals[prim.Normal]);
                if (HasTexCoords)
                    vertex.SetData(VertexUsageType.TextureCoordinate, 0, TexCoords[prim.TexCoord]);

                return vertex;
            }

            public static VerticesHolder Create(COLLADAMesh mesh)
            {
                var holder = new VerticesHolder();

                // this isn't how it's supposed to work,
                // but I'll probably fix this eventually (or not)
                foreach (var source in mesh.Sources)
                {
                    var sourceId = source.Info.Id;
                    var accessor = source.Technique.Accessor;

                    var count = accessor.Count;
                    var stride = accessor.Stride;

                    if (sourceId.Contains("-positions"))
                    {
                        ReadVectorData(source, ReadVector3, out holder.Positions);
                    }
                    else if (sourceId.Contains("-normals"))
                    {
                        ReadVectorData(source, ReadVector3, out holder.Normals);
                    }
                    else if (sourceId.Contains("-map"))
                    {
                        ReadVectorData(source, ReadVector2, out holder.TexCoords);
                    }
                    else
                    {
                        Debug.WriteLine($"WARNING: Could not determine source type for '{sourceId}', skipping...");
                    }
                }

                return holder;
            }
        }

        public struct PolyMesh
        {
            public struct Primitive
            {
                public int Position;
                public int Normal;
                public int TexCoord;
            }

            public Primitive[] Primitives;
            
            public int MaterialId;

            public int NumPrimitives;

            public static PolyMesh Create(COLLADAPolyList polyList)
            {
                var count = polyList.Count;

                var sPos = -1;
                var sNor = -1;
                var sTex = -1;
                
                foreach (var input in polyList.Inputs)
                {
                    var offset = input.Offset;

                    switch (offset)
                    {
                    case 0:
                        sPos = offset;
                        break;
                    case 1:
                        sNor = offset;
                        break;
                    case 2:
                        if (input.Set == 0)
                            sTex = offset;
                        break;
                    }
                }

                var hasPositions = (sPos != -1);
                var hasNormals = (sNor != -1);
                var hasTexCoords = (sTex != -1);

                if (!hasPositions || !hasNormals)
                    throw new InvalidOperationException("Can't find required semantic(s) in poly list!");
                
                var prims = new Primitive[count * 3];
                var minIndex = 0;

                for (int p = 0; p < count; p++)
                {
                    var vc = polyList.VCount[p];

                    if (vc != 3)
                        throw new InvalidOperationException("Non-triangulated poly list!");

                    var idx = (p * vc);

                    for (int t = 0; t < vc; t++)
                    {
                        var prim = new Primitive() {
                            Position = polyList.Indices[minIndex + sPos],
                            Normal = polyList.Indices[minIndex + sNor],
                        };

                        if (hasTexCoords)
                            prim.TexCoord = polyList.Indices[minIndex + sTex];

                        prims[idx + t] = prim;

                        minIndex += polyList.Inputs.Count;
                    }
                }

                return new PolyMesh() {
                    Primitives = prims,
                    NumPrimitives = count,
                    MaterialId = -1, // apply after
                };
            }
        }

        public class IndicesHolder
        {
            public List<short> Indices = new List<short>();

            public int Stride
            {
                get { return 3; }
            }
            
            public int Count
            {
                get { return Indices.Count; }
            }

            public int NumPrimitives
            {
                get { return (Count * Stride); }
            }

            public IndexData Compile()
            {
                return new IndexData(Count) {
                    Buffer = Indices.ToArray(),
                };
            }
        }
        
        public class GeometryHolder
        {
            public VerticesHolder Vertices;

            public List<PolyMesh> Meshes;

            public LodInstance SubModel;

            public void LoadMesh(COLLADAMesh colladaMesh, COLLADAMaterialLibrary mtlLib)
            {
                Meshes = new List<PolyMesh>();

                Vertices = VerticesHolder.Create(colladaMesh);

                foreach (var polyList in colladaMesh.PolyLists)
                {
                    var polyMesh = PolyMesh.Create(polyList);

                    // assign material
                    polyMesh.MaterialId = mtlLib.Materials.FindIndex((m) => m.Info.Id == polyList.Material);

                    if (polyMesh.MaterialId == -1)
                    {
                        Debug.WriteLine($"WARNING: material '{polyList.Material}' was not found! Defaulting to 0");
                        polyMesh.MaterialId = 0;
                    }

                    Meshes.Add(polyMesh);
                }
            }

            public List<SubModel> CompileMeshes(VertexBuffer vertexBuffer, IndicesHolder indexBuffer, bool shadowModelHack)
            {
                var meshes = new List<SubModel>();

                var vertices = vertexBuffer.Vertices;
                var indices = indexBuffer.Indices;

                var lodInstance = SubModel.Parent;
                var model = lodInstance.Parent;

                // reset the old meshes (causes crash if we don't do this)
                SubModel.SubModels = new List<SubModel>();
                
                foreach (var polyMesh in Meshes)
                {
                    var primitives = polyMesh.Primitives;
                    var numPrims = polyMesh.NumPrimitives;

                    var vertexOffset = vertexBuffer.Count;
                    var indexOffset = indexBuffer.Count;

                    for (int p = 0; p < numPrims; p++)
                    {
                        var primIdx = (p * 3);

                        var tmp = new short[3];

                        for (int t = 0; t < 3; t++)
                        {
                            var prim = primitives[primIdx + t];

                            var vertex = Vertices.CreateVertex(vertexBuffer, prim);
                            var index = (short)vertices.Count;

                            var vIdx = vertices.FindIndex(vertexOffset, (v) => {
                                return v.PossiblyEqual(ref vertex);
                            });

                            if (vIdx != -1)
                            {
                                // reuse existing data
                                //Debug.WriteLine($"{index} -> {vertexOffset}[{vIdx}]");

                                index = (short)vIdx;
                            }
                            else
                            {
                                vertices.Add(vertex);
                            }

                            tmp[t] = index;
                        }

                        indices.Add(tmp[0]);
                        indices.Add(tmp[1]);
                        indices.Add(tmp[2]);
                    }

                    var mesh = new SubModel() {
                        Model = model,
                        LodInstance = SubModel,

                        PrimitiveType = PrimitiveType.TriangleList,

                        VertexOffset = vertexOffset,
                        VertexCount = (vertices.Count - vertexOffset),

                        IndexOffset = indexOffset,
                        IndexCount = numPrims,

                        MaterialId = polyMesh.MaterialId,
                        SourceUID = 0xFFFD, // assume it's a self-contained vehicle texture
                    };

                    if (shadowModelHack)
                    {
                        mesh.VertexOffset = 0;
                        mesh.VertexCount = 0;
                        mesh.IndexOffset = 0;
                        mesh.IndexCount = 0;
                    }

                    //for (int i = 0; i < numPrims; i++)
                    //{
                    //    var offset = indexOffset + (i * 3);
                    //
                    //    int i0 = indices[offset];
                    //    int i1 = indices[offset + 1];
                    //    int i2 = indices[offset + 2];
                    //
                    //    Debug.WriteLine($"t {i0} {i1} {i2}");
                    //}
                    //
                    //Debug.WriteLine("----");

                    meshes.Add(mesh);
                }

                SubModel.SubModels.AddRange(meshes);

                return meshes;
            }

            public GeometryHolder()
            {
                Meshes = new List<PolyMesh>();
            }

            public GeometryHolder(LodInstance subModel)
                : base()
            {
                SubModel = subModel;
            }
        }

        public class MaterialHolder
        {
            public string Name;
            public string TextureFile;

            public Vector4 Color;
            
            public MaterialHolder()
            {
                Name = String.Empty;
                TextureFile = String.Empty;

                Color = new Vector4(1.0f, 1.0f, 1.0f, 1.0f);
            }
        }

        public string UID { get; set; }

        public EffectType EffectType { get; set; }

        public List<Model> Models { get; set; }
        public List<LodInstance> SubModels { get; set; }

        public List<GeometryHolder> Geometries { get; set; }
        public List<MaterialHolder> Materials { get; set; }
        
        private static int GetVertexDeclType(EffectType effectType)
        {
            switch (effectType)
            {
            case EffectType.Vehicle:
                return 5;
            }

            throw new NotImplementedException($"Effect type '{effectType.ToString()}' is not implemented yet.");
        }

        private static int GetPartSlotIndex(string slotType)
        {
            switch (slotType)
            {
            case "H":       return 0;
            case "M":       return 1;
            case "L":       return 2;
            case "VL":      return 4;
            case "SHADOW":  return 5;
            }

            return -1;
        }

        private static int GetPartSlotType(string slotType)
        {
            switch (slotType)
            {
            case "H":       return 0x1E;
            case "M":       return 0x1C;
            case "L":       return 0x18;
            case "VL":      return 1;
            case "SHADOW":  return 0;
            }

            return -1;
        }
        
        private static Vector4 GetVectorFromTransform(float[] transform, int offset)
        {
            return new Vector4() {
                X = transform[offset + 0],
                Y = transform[offset + 1],
                Z = transform[offset + 2],
                W = transform[offset + 3],
            };
        }

        private static byte[] GetBufferFromHexString(string input)
        {
            var result = new byte[input.Length / 2];

            int offset = (input.Length - 2);
            var idx = 0;

            while (offset >= 0)
            {
                var val = input.Substring(offset, 2);

                result[idx++] = byte.Parse(val, NumberStyles.HexNumber);

                offset -= 2;
            }

            return result;
        }

        public bool LoadCOLLADA(COLLADADocument collada)
        {
            COLLADAVisualScene sceneRoot = null;

            foreach (var scene in collada.SceneLibrary.Scenes)
            {
                if (scene.Info.Id == "Scene")
                {
                    sceneRoot = scene;
                    break;
                }
            }

            if (sceneRoot == null)
                throw new InvalidOperationException("Could not find scene root!");

            if (sceneRoot.Nodes.Count != 1)
                throw new InvalidOperationException("Invalid scene -- expected a single root node");

            var rootNode = sceneRoot.Nodes[0];
            var rootId = rootNode.Info.Id;
            
            if (!rootId.StartsWith("root_"))
                throw new InvalidOperationException("Root node in scene does not conform to naming convention.");

            UID = rootId.Split('_')[1].PadLeft(16, '0');
            
            Debug.WriteLine($"Model UID: {UID}");

            if (rootNode.GeometryInstances.Count != 0)
                throw new InvalidOperationException("Root node cannot contain geometry.");

            var partIdx = 0;
            
            foreach (var childPart in rootNode.Children)
            {
                if (childPart.GeometryInstances.Count != 0)
                    throw new InvalidOperationException($"Child {partIdx} ('{childPart.Info.Id}') in root node is malformed.");

                var partsGroup = new Model() {
                    VertexType = -1
                };

                var parts = partsGroup.Lods;

                Models.Add(partsGroup);

                var geomQueue = new COLLADAGeometryInstance[7];

                foreach (var lodInstance in childPart.Children)
                {
                    var objName = lodInstance.Info.Id;

                    if (lodInstance.GeometryInstances.Count != 1)
                    {
                        Debug.WriteLine($"WARNING: Non-mesh object '{objName}' in child {partIdx} ('{childPart.Info.Id}'), skipping...");
                        continue;
                    }

                    var slotType = objName.Split('_').Last();

                    var slotIdx = GetPartSlotIndex(slotType);

                    if (slotIdx == -1)
                        throw new InvalidOperationException($"Could not determine slot index for '{objName}'!");
                    
                    // we need to build the buffers in the proper order
                    // so we'll process them after we get the basic layout setup
                    geomQueue[slotIdx] = lodInstance.GeometryInstances[0];

                    var partDef = new Lod(slotIdx) {
                        Parent = partsGroup,
                        Type = GetPartSlotType(slotType),
                    };

                    parts[slotIdx] = partDef;

                    var subModel = new LodInstance() {
                        Parent = partDef
                    };

                    partDef.Instances.Add(subModel);
                    
                    var transform = lodInstance.Matrix.Values;

                    for (int t = 0; t < 4; t++)
                        subModel.Transform[t] = GetVectorFromTransform(transform, (t * 4));
                }

                // gotta make yet another fucking queue
                var geomLists = new List<GeometryHolder>[7];

                for (int i = 0; i < 7; i++)
                    geomLists[i] = new List<GeometryHolder>();

                for (int p = 0; p < 7; p++)
                {
                    var part = partsGroup.Lods[p];

                    // nothing to see here
                    if (part == null)
                        continue;

                    var subModel = part.Instances[0];

                    // correct order
                    SubModels.Add(subModel);

                    var geomInst = geomQueue[p];

                    // very bad!
                    if (geomInst == null)
                        throw new InvalidOperationException($"Geometry is MISSING for part {p + 1}!");

                    var geom = collada.GeometryLibrary[geomInst.Url];

                    if (geom == null)
                        throw new InvalidOperationException($"Geometry URL '{geomInst.Url}' not found!");

                    var mesh = geom.Mesh;
                    var geometry = new GeometryHolder(subModel);

                    geometry.LoadMesh(mesh, collada.MaterialLibrary);

                    geomLists[p].Add(geometry);
                }

                foreach (var geomList in geomLists)
                {
                    Geometries.AddRange(geomList);
                    Geometries.Add(null);
                }
            }

            Materials = new List<MaterialHolder>();

            // now parse materials in ascending order (material id's were already assigned by this point)
            foreach (var material in collada.MaterialLibrary.Materials)
            {
                var mtl = new MaterialHolder() {
                    Name = material.Info.Name
                };

                var effect = collada.EffectLibrary.Effects.Find((e) => e.Info.Id == material.EffectInstance.Url.Substring(1));

                var profile = effect.Profile;
                var technique = profile.Technique;
                var shader = technique.Shader;

                // find texture...
                // (did I mention COLLADA is a pain in the ass?)
                if (!String.IsNullOrEmpty(shader.Diffuse.Value.Texture))
                {
                    var sm = profile.Params.Find((p) => p.SubId == shader.Diffuse.Value.Texture)?.Param as COLLADASampler2DParamValue;

                    if (sm != null)
                    {
                        var sampler = sm.Value;
                        var sr = profile.Params.Find((n) => n.SubId == sampler.Source)?.Param as COLLADASurfaceParamValue;

                        if (sr != null)
                        {
                            var surface = sr.Value;

                            if (surface.Type == "2D")
                            {
                                var im = collada.ImageLibrary.Images.Find((g) => g.Id == surface.InitFrom);

                                mtl.TextureFile = Path.GetFullPath(im.InitFrom);
                            }
                        }
                    }
                }
                else
                {
                    // try making a texture based on the color ;)
                    mtl.Color = shader.Diffuse.Value.Color;
                }

                Materials.Add(mtl);
            }

            return true;
        }
        
        public ModelPackage ToModelPackage()
        {
            var modelPackage = SpoolableResourceFactory.Create<ModelPackage>();
            
            var vDeclType = GetVertexDeclType(EffectType);

            // HACK: assume Driv3r format!
            var vertexBuffer = VertexBuffer.Create(0, vDeclType);
            var indexBuffer = new IndicesHolder();

            var meshList = new List<SubModel>();

            var vertexBaseOffset = vertexBuffer.Count;
            
            foreach (var geom in Geometries)
            {
                if (geom == null)
                {
                    vertexBaseOffset = vertexBuffer.Count;
                    continue;
                }

                var subModel = geom.SubModel;
                var lodInstance = subModel.Parent;
                var model = lodInstance.Parent;
                
                // fixup vertex decl type if needed
                if (model.VertexType == -1)
                {
                    model.VertexBuffer = vertexBuffer;
                    model.VertexType = (short)vDeclType;
                    model.Flags = 1; // has vertex buffer
                }
                
                // populates vertex/index buffers and adds them to the submodel
                var meshes = geom.CompileMeshes(vertexBuffer, indexBuffer, (lodInstance.ID == 5));
                
                meshList.AddRange(meshes);

                // HACK: apply model package to meshes (REWRITE EVERYTHING!!!)
                foreach (var mesh in meshes)
                    mesh.ModelPackage = modelPackage;
            }
            
            // finally...compile the model package
            modelPackage.Models = Models;
            modelPackage.LodInstances = SubModels;
            modelPackage.SubModels = meshList;

            modelPackage.VertexBuffers = new List<VertexBuffer>() {
                vertexBuffer
            };

            modelPackage.IndexBuffer = indexBuffer.Compile();

            // temporarily force vehicle package
            modelPackage.UID = 0xFF;

            // generate uid shit
            var uidBuffer = GetBufferFromHexString(UID);

            var uid = BitConverter.ToInt32(uidBuffer, 0);
            var handle = BitConverter.ToInt32(uidBuffer, 4);

            // fixup models
            foreach (var model in Models)
            {
                model.UID = new UID(uid, handle);

                // no fucking clue what this is
                // with my luck, it's just padding :/
                model.Unknown = new Vector4() {
                    X = 0.0f,
                    Y = 0.0f,
                    Z = 0.0f,
                    W = 1.0f,
                };

                // TODO: properly calculate bounding box
                for (int i = 0; i < 8; i++)
                {
                    model.Transform[i] = new Vector4() {
                        X = 0.0f,
                        Y = 0.0f,
                        Z = 0.0f,
                        W = 1.0f,
                    };
                }
            }
            
            modelPackage.Materials = new List<MaterialDataPC>();
            modelPackage.Substances = new List<SubstanceDataPC>();
            modelPackage.Textures = new List<TextureDataPC>();

            // lastly, apply materials
            foreach (var material in Materials)
            {
                var tex = new TextureDataPC() {
                    UID = 0x01010101
                };

                tex.Buffer = File.Exists(material.TextureFile)
                    ? File.ReadAllBytes(material.TextureFile)
                    : DDSUtils.MakeRGBATexture(material.Color);

                var header = default(DDSHeader);

                if (DDSUtils.GetHeaderInfo(tex.Buffer, ref header))
                {
                    tex.Type = DDSUtils.GetTextureType(header.PixelFormat.FourCC);

                    tex.Width = header.Width;
                    tex.Height = header.Height;
                }
                else
                {
                    // TODO: throw exception and handle this
                    Debug.WriteLine("**** UNKNOWN TEXTURE FORMAT ****");
                }

                tex.Hash = (int)Memory.GetCRC32(tex.Buffer);

                var sub = new SubstanceDataPC() {
                    Flags = 0x406, // HACK: generic vehicle texture
                    Textures = new List<TextureDataPC>() {
                        tex
                    },
                };

                var mtl = new MaterialDataPC() {
                    Substances = new List<SubstanceDataPC>() {
                        sub
                    },
                };

                modelPackage.Materials.Add(mtl);
                modelPackage.Substances.Add(sub);
                modelPackage.Textures.Add(tex);
            }

            var resource = (ISpoolableResource)modelPackage;

            resource.Spooler = new SpoolableBuffer() {
                Context = (int)ChunkType.ModelPackagePC,
                Alignment = SpoolerAlignment.Align4096,
                Version = 6,
                Description = "Custom model package",
            };
            
            return modelPackage;
        }

        public ModelConverter()
        {
            Models = new List<Model>();
            SubModels = new List<LodInstance>();
            Geometries = new List<GeometryHolder>();
        }
    }
}
