﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using System.Xml;
using System.Xml.Linq;

namespace Audiose
{
    public enum GSDFormatType
    {
        Invalid = -1,

        BK01,
        BK31,
    }
    
    public struct Identifier
    {
        int m_value;
        
        public static implicit operator int(Identifier id)
        {
            return id.m_value;
        }

        public static implicit operator Identifier(int id)
        {
            return new Identifier(id);
        }

        public static implicit operator Identifier(string id)
        {
            if (id.Length != 4)
                throw new ArgumentException("Identifier MUST be 4 characters long!", nameof(id));

            var c1 = id[0];
            var c2 = id[1];
            var c3 = id[2];
            var c4 = id[3];

            return new Identifier((c4 << 24) | (c3 << 16) | (c2 << 8) | (c1 << 0));
        }

        public override string ToString()
        {
            var str = "";

            if (m_value > 0)
            {
                for (int b = 0; b < 4; b++)
                {
                    var c = (m_value >> (b * 8)) & 0xFF;
                    if (c != 0)
                        str += (char)c;
                }
            }

            return str;
        }

        private Identifier(int id)
        {
            m_value = id;
        }        
    }
    
    public interface IRIFFChunkData
    {
        Identifier Identifier { get; }
        int Size { get; }

        Type Type { get; }
    }

    public struct AudioFormatChunk : IRIFFChunkData
    {
        public Identifier Identifier
        {
            get { return "fmt "; }
        }

        public int Size
        {
            get { return 0x10 /* sizeof(AudioFormatChunk) */; }
        }

        Type IRIFFChunkData.Type
        {
            get { return typeof(AudioFormatChunk); }
        }
        
        public short AudioFormat;
        public short NumChannels;

        public int SampleRate;
        public int ByteRate;

        public short BlockAlign;
        public short BitsPerSample;

        public AudioFormatChunk(int numChannels, int sampleRate)
            : this(1, numChannels, sampleRate, 16)
        { }

        public AudioFormatChunk(int numChannels, int sampleRate, int bitsPerSample)
            : this(1, numChannels, sampleRate, bitsPerSample)
        { }

        public AudioFormatChunk(int audioFormat, int numChannels, int sampleRate, int bitsPerSample)
        {
            AudioFormat = (short)(audioFormat & 0xFFFF);
            NumChannels = (short)(numChannels & 0xFFFF);
            SampleRate = sampleRate;
            BitsPerSample = (short)(bitsPerSample & 0xFFFF);

            BlockAlign = (short)(NumChannels * (BitsPerSample / 8));
            ByteRate = (SampleRate * 2);
        }
    }
    
    public static class RIFF
    {
        public static readonly Identifier RIFFIdentifier = "RIFF";
        public static readonly Identifier WAVEIdentifier = "WAVE";

        public static readonly Identifier DATAIdentifier = "data";

        public static readonly int ChunkHeaderSize = 0x8;

        public static int ReadRIFF(this Stream stream)
        {
            if (stream.ReadInt32() == RIFFIdentifier)
            {
                var size = stream.ReadInt32();

                if (stream.ReadInt32() == WAVEIdentifier)
                    return (size - 4);
            }

            return -1;
        }
        
        public static bool FindChunk(this Stream stream, Identifier id)
        {
            while (stream.ReadInt32() != id)
            {
                // oh shit
                if ((stream.Position + 1) > stream.Length)
                    return false;

                var size = stream.ReadInt32();
                stream.Position += size;
            }
            return true;
        }

        public static byte[] ReadChunk(this Stream stream, Identifier id)
        {
            if (stream.FindChunk(id))
            {
                var size = stream.ReadInt32();
                return stream.ReadBytes(size);
            }

            // reset position
            stream.Position -= 4;
            return null;
        }

        public static bool ReadChunk<T>(this Stream stream, ref T chunk)
            where T : struct, IRIFFChunkData
        {
            var buffer = stream.ReadChunk(chunk.Identifier);

            if (buffer == null)
                return false;
            if (buffer.Length != chunk.Size)
                throw new InvalidOperationException("What in the F$@%??");

            var ptr = Marshal.AllocHGlobal(chunk.Size);

            Marshal.Copy(buffer, 0, ptr, chunk.Size);

            chunk = (T)Marshal.PtrToStructure(ptr, typeof(T));
            Marshal.FreeHGlobal(ptr);

            return true;
        }

        public static byte[] ReadData(this Stream stream)
        {
            return stream.ReadChunk(DATAIdentifier);
        }
        
        public static void WriteRIFF(this Stream stream, byte[] dataBuffer, params IRIFFChunkData[] chunks)
        {
            var dataSize = dataBuffer.Length;
            var riffSize = (dataSize + ChunkHeaderSize + 0x4 /* + WAVE */);

            foreach (var chunk in chunks)
                riffSize += (chunk.Size + ChunkHeaderSize);
            
            stream.SetLength(riffSize + ChunkHeaderSize);

            stream.WriteChunk(RIFFIdentifier, riffSize);
            stream.Write(WAVEIdentifier);

            foreach (var chunk in chunks)
            {
                stream.WriteChunk(chunk.Identifier, chunk.Size);

                var data = new byte[chunk.Size];
                var ptr = GCHandle.Alloc(chunk, GCHandleType.Pinned);

                Marshal.Copy(ptr.AddrOfPinnedObject(), data, 0, chunk.Size);
                ptr.Free();
                
                stream.Write(data);
            }

            stream.WriteChunk(DATAIdentifier, dataBuffer);
        }
        
        public static void WriteChunk(this Stream stream, Identifier id, int chunkSize)
        {
            stream.Write(id);
            stream.Write(chunkSize);
        }

        public static void WriteChunk(this Stream stream, Identifier id, byte[] buffer)
        {
            var chunkSize = buffer.Length;

            stream.WriteChunk(id, chunkSize);
            stream.Write(buffer, 0, chunkSize);
        }

        public static void WriteChunk<T>(this Stream stream, Identifier id, T chunk)
        {
            var chunkSize = Marshal.SizeOf(typeof(T));

            stream.WriteChunk(id, chunkSize);
            stream.Write(chunk, chunkSize);
        }

        public static void WriteChunk<T>(this Stream stream, T chunk)
            where T : struct, IRIFFChunkData
        {
            stream.WriteChunk(chunk.Identifier, chunk.Size);
            stream.Write(chunk);
        }
    }

    public interface ISoundBankInfoDetail
    {
        GSDFormatType FormatType { get; }

        int HeaderSize { get; }
        int SampleSize { get; }

        int SampleChannelFlags { get; }

        void SetDataInfo(int offset, int size);

        int DataOffset { get; }
        int DataSize { get; }
        
        void Copy(SoundBank bank);
        void CopyTo(SoundBank bank);
    }

    public struct SoundBankInfo1 : ISoundBankInfoDetail
    {
        GSDFormatType ISoundBankInfoDetail.FormatType
        {
            get { return GSDFormatType.BK01; }
        }

        int ISoundBankInfoDetail.HeaderSize
        {
            get { return 0xC; }
        }

        int ISoundBankInfoDetail.SampleSize
        {
            get { return 0x14; }
        }

        int ISoundBankInfoDetail.SampleChannelFlags
        {
            get { return 0x80; }
        }

        int ISoundBankInfoDetail.DataOffset
        {
            get { return DataOffset; }
        }

        int ISoundBankInfoDetail.DataSize
        {
            get { return DataSize; }
        }

        public int NumSamples;

        public int DataOffset;
        public int DataSize;

        void ISoundBankInfoDetail.SetDataInfo(int offset, int size)
        {
            DataOffset = offset;
            DataSize = size;
        }

        public void Copy(SoundBank bank)
        {
            NumSamples = bank.Samples.Count;
        }

        public void CopyTo(SoundBank bank)
        {
            bank.Samples = new List<SoundSample>(NumSamples);
        }
    }

    public struct SoundBankInfo3 : ISoundBankInfoDetail
    {
        GSDFormatType ISoundBankInfoDetail.FormatType
        {
            get { return GSDFormatType.BK31; }
        }

        int ISoundBankInfoDetail.HeaderSize
        {
            get { return 0x10; }
        }

        int ISoundBankInfoDetail.SampleSize
        {
            get { return 0x10; }
        }

        int ISoundBankInfoDetail.SampleChannelFlags
        {
            get { return 0x10; }
        }

        int ISoundBankInfoDetail.DataOffset
        {
            get { return DataOffset; }
        }

        int ISoundBankInfoDetail.DataSize
        {
            get { return DataSize; }
        }

        public int Index;
        public int NumSamples;

        public int DataOffset;
        public int DataSize;

        void ISoundBankInfoDetail.SetDataInfo(int offset, int size)
        {
            DataOffset = offset;
            DataSize = size;
        }

        public void Copy(SoundBank bank)
        {
            Index = bank.Index;
            NumSamples = bank.Samples.Count;
        }

        public void CopyTo(SoundBank bank)
        {
            bank.Index = Index;
            bank.Samples = new List<SoundSample>(NumSamples);
        }
    }

    public struct SoundSampleInfo
    {
        public static readonly int SizeOf = Marshal.SizeOf(typeof(SoundSampleInfo));

        public int Offset;
        public int Size;

        public ushort SampleRate;

        public byte Flags;
        public byte Unk_0B;

        public int Unk_0C;
    }

    public struct GSDHeader
    {
        public const int ID_BK01 = 0x424B3130; // '01KB'
        public const int ID_BK31 = 0x31334B42; // 'BK31'

        public int Identifier;
        public int NumBanks;

        public int Alignment
        {
            get { return 2048; }
        }
        
        public int ListOffset
        {
            get { return 8; }
        }

        public int ListSize
        {
            get { return NumBanks * 4; }
        }

        public int FileSizeOffset
        {
            get { return ListOffset + ListSize; }
        }

        public int Size
        {
            get { return FileSizeOffset + 4; }
        }
        
        public GSDFormatType FormatType
        {
            get
            {
                switch (Identifier)
                {
                case ID_BK01: return GSDFormatType.BK01;
                case ID_BK31: return GSDFormatType.BK31;
                }

                return GSDFormatType.Invalid;
            }
        }

        private static int GetIdentifier(GSDFormatType type)
        {
            switch (type)
            {
            case GSDFormatType.BK01: return ID_BK01;               
            case GSDFormatType.BK31: return ID_BK31;
            }

            return -1;
        }

        public Type GetSoundBankType(GSDFormatType type)
        {
            switch (type)
            {
            case GSDFormatType.BK01: return typeof(SoundBankInfo1);
            case GSDFormatType.BK31: return typeof(SoundBankInfo3);
            }
            
            throw new InvalidOperationException("Cannot determine sound bank type.");
        }

        public ISoundBankInfoDetail CreateSoundBank(GSDFormatType type)
        {
            var bankType = GetSoundBankType(type);
            return (ISoundBankInfoDetail)Activator.CreateInstance(bankType);
        }

        public ISoundBankInfoDetail CreateSoundBank(GSDFormatType type, SoundBank bank)
        {
            var result = CreateSoundBank(type);
            result.Copy(bank);

            return result;
        }

        public GSDHeader(GSDFormatType type, int numBanks)
        {
            Identifier = GetIdentifier(type);
            NumBanks = numBanks;
        }
    }

    public interface ISerializer<T>
    {
        void Serialize(T input);
        void Deserialize(T output);
    }

    public class SoundBank : ISerializer<XmlDocument>
    {
        public int Index { get; set; }

        public bool IsNull
        {
            get { return Index == -1; }
        }
        
        public List<SoundSample> Samples { get; set; }

        public void Serialize(XmlDocument xml)
        {
            var bank = xml.CreateElement("SoundBank");
            bank.SetAttribute("Index", $"{Index:D}");

            if (!IsNull)
            {
                foreach (var sample in Samples)
                    sample.Serialize(bank);
            }
            
            xml.AppendChild(bank);
        }

        public void Deserialize(XmlDocument xml)
        {
            var elem = xml.DocumentElement;

            if ((elem == null) || (elem.Name != "SoundBank"))
                throw new InvalidOperationException("Not a SoundBank node!");

            var index = elem.GetAttribute("Index");

            if (String.IsNullOrEmpty(index))
                throw new XmlException("Malformed SoundBank XML data, cannot determine the index!");

            Index = int.Parse(index);
            Samples = new List<SoundSample>();

            foreach (var node in elem.ChildNodes.OfType<XmlElement>())
            {
                var sample = new SoundSample();
                sample.Deserialize(node);

                Samples.Add(sample);
            }
        }
    }

    public class SoundSample : ISerializer<XmlNode>
    {
        // relative path (e.g. '00.wav' and NOT 'c:\path\to\file\00.wav')
        public string FileName { get; set; }

        public int NumChannels { get; set; }
        public int SampleRate { get; set; }

        public int Flags { get; set; }

        public int Unknown1 { get; set; }
        public int Unknown2 { get; set; }

        public byte[] Buffer { get; set; }

        public static explicit operator AudioFormatChunk(SoundSample sample)
        {
            return new AudioFormatChunk(sample.NumChannels, sample.SampleRate);
        }

        public void Serialize(XmlNode xml)
        {
            var xmlDoc = (xml as XmlDocument) ?? xml.OwnerDocument;
            var elem = xmlDoc.CreateElement("Sample");

            elem.SetAttribute("File", FileName);
            elem.SetAttribute("Flags", $"{Flags:D}");
            elem.SetAttribute("Unk1", $"{Unknown1:D}");
            elem.SetAttribute("Unk2", $"{Unknown2:D}");

            xml.AppendChild(elem);
        }

        public void Deserialize(XmlNode xml)
        {
            if (xml.Name != "Sample")
                throw new InvalidOperationException("Not a Sample node!");

            foreach (XmlAttribute attr in xml.Attributes)
            {
                var value = attr.Value;

                switch (attr.Name)
                {
                case "File":
                    FileName = value;
                    break;
                case "Flags":
                    Flags = int.Parse(value);
                    break;
                case "Unk1":
                    Unknown1 = int.Parse(value);
                    break;
                case "Unk2":
                    Unknown2 = int.Parse(value);
                    break;
                default:
                    Console.WriteLine($">> Unknown attribute '{attr.Name}', skipping...");
                    break;
                }
            }

            if (String.IsNullOrEmpty(FileName))
                throw new InvalidOperationException("Empty samples are NOT allowed!");
        }
    }

    public class GSDFile
    {
        public List<SoundBank> Banks { get; set; }

        public GSDFormatType Type { get; set; }

        public void DumpAllBanks(string outDir)
        {
            var gsdXml = new XmlDocument();
            var gsdElem = gsdXml.CreateElement("GameSoundDatabase");

            gsdElem.SetAttribute("Type", $"{Type:D}");

            for (int i = 0; i < Banks.Count; i++)
            {
                var bank = Banks[i];
                var bankName = $"{i:D2}";
                var bankPath = Path.Combine("Banks", bankName);

                var bankDir = Path.Combine(outDir, bankPath);
                
                if (!Directory.Exists(bankDir))
                    Directory.CreateDirectory(bankDir);

                var xmlFile = Path.Combine(bankDir, "bank.xml");

                // write bank xml
                var bankXml = new XmlDocument();
                bank.Serialize(bankXml);

                bankXml.Save(xmlFile);

                if (!bank.IsNull)
                {
                    for (int s = 0; s < bank.Samples.Count; s++)
                    {
                        var sample = bank.Samples[s];
                        var sampleFile = Path.Combine(bankDir, sample.FileName);

                        using (var fs = File.Open(sampleFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            var fmtChunk = (AudioFormatChunk)sample;

                            RIFF.WriteRIFF(fs, sample.Buffer, fmtChunk);
                        }
                    }
                }

                // append to GSD xml
                var bankElem = gsdXml.CreateElement("SoundBank");
                
                bankElem.SetAttribute("Index", $"{i:D}");
                bankElem.SetAttribute("File", Path.Combine(bankPath, "bank.xml"));

                gsdElem.AppendChild(bankElem);
            }

            gsdXml.AppendChild(gsdElem);
            gsdXml.Save(Path.Combine(outDir, "config.xml"));
        }

        public void LoadXml(string filename)
        {
            var root = Path.GetDirectoryName(filename);

            var xml = new XmlDocument();
            xml.Load(filename);

            var gsdElem = xml.DocumentElement;

            if ((gsdElem == null) || (gsdElem.Name != "GameSoundDatabase"))
                throw new InvalidOperationException("Not a GameSoundDatabase node!");

            var type = int.Parse(gsdElem.GetAttribute("Type"));

            if (!Enum.IsDefined(typeof(GSDFormatType), type))
                throw new InvalidOperationException($"Unknown sound database type '{type}'!");
            
            Type = (GSDFormatType)type;
            
            var banks = new List<SoundBank>();
            var bankRefs = new Dictionary<int, SoundBank>();
            
            foreach (var node in gsdElem.ChildNodes.OfType<XmlElement>())
            {
                if (node.Name != "SoundBank")
                    throw new InvalidOperationException($"What the hell do I do with a '{node.Name}' element?!");
                
                var index = node.GetAttribute("Index");
                var file = node.GetAttribute("File");

                if (String.IsNullOrEmpty(file))
                    throw new InvalidOperationException("Cannot parse embedded or empty SoundBank nodes!");

                var bankDir = Path.GetDirectoryName(file);
                var bankFile = Path.Combine(root, file);

                var bankIdx = int.Parse(index);
                
                if (!File.Exists(bankFile))
                    throw new InvalidOperationException($"SoundBank file '{bankFile}' is missing!");

                var bankXml = new XmlDocument();
                bankXml.Load(bankFile);

                var bank = new SoundBank();
                bank.Deserialize(bankXml);

                if (bank.Index != bankIdx)
                {
                    if (!bank.IsNull)
                    {
                        // reset samples!
                        bank.Samples = new List<SoundSample>();
                        bankRefs.Add(bankIdx, bank);
                    }
                }
                else
                {
                    // fill sample buffers
                    foreach (var sample in bank.Samples)
                    {
                        var sampleFile = Path.Combine(root, bankDir, sample.FileName);

                        using (var fs = File.Open(sampleFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            if (fs.ReadRIFF() == -1)
                                throw new InvalidOperationException("RICKY! WHAT HAVE YOU DONE!");

                            var fmtChunk = new AudioFormatChunk();

                            if (fs.ReadChunk(ref fmtChunk))
                            {
                                var data = fs.ReadData();

                                if (data == null)
                                    throw new InvalidOperationException("Empty WAV files are NOT allowed!");

                                sample.NumChannels = fmtChunk.NumChannels;
                                sample.SampleRate = fmtChunk.SampleRate;
                                sample.Buffer = data;
                            }
                            else
                            {
                                throw new InvalidOperationException("Holy shit, what have you done to this WAV file?!");
                            }           
                        }
                    }
                }

                banks.Add(bank);
            }

            var gsdBanks = new SoundBank[banks.Count];

            for (int i = 0; i < gsdBanks.Length; i++)
            {
                var bank = banks[i];
                gsdBanks[bank.Index] = bank;
            }

            for (int i = 0; i < gsdBanks.Length; i++)
            {
                if (gsdBanks[i] == null)
                    throw new NullReferenceException($"Bank {i} / {gsdBanks.Length} is MISSING! Double check bank index numbers and try again.");
            }

            Banks = new List<SoundBank>(gsdBanks);
            
            // resolve bank references
            foreach (var bankRef in bankRefs)
            {
                var bank = bankRef.Value;
                var copyBank = Banks[bank.Index];
            
                Banks[bankRef.Key] = copyBank;
            }
        }

        public void LoadBinary(string filename)
        {
            using (var fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var header = fs.Read<GSDHeader>();

                if (header.FormatType == GSDFormatType.Invalid)
                    throw new InvalidOperationException("Not a GSD file!");

                Type = header.FormatType;
                
                Banks = new List<SoundBank>(header.NumBanks);
                
                for (int i = 0; i < header.NumBanks; i++)
                {
                    fs.Position = (header.ListOffset + (i * 4));

                    var offset = fs.ReadInt32();
                    fs.Position = offset;

                    ISoundBankInfoDetail bankDetail = null;

                    if (fs.Position == fs.Length)
                    {
                        // completely empty soundbank!
                        var emptyBank = new SoundBank() {
                            Index = -1,
                        };

                        Banks.Add(emptyBank);
                        continue;
                    }

                    switch (Type)
                    {
                    case GSDFormatType.BK01:
                        var info1 = fs.Read<SoundBankInfo1>();
                        info1.DataOffset += offset;

                        bankDetail = info1;
                        break;
                    case GSDFormatType.BK31:
                        var info3 = fs.Read<SoundBankInfo3>();
                        info3.DataOffset += offset;

                        bankDetail = info3;
                        break;
                    }
                    
                    var bank = new SoundBank() {
                        Index = i, // may be overridden by copy
                    };

                    bankDetail.CopyTo(bank);
                    
                    // don't read in duplicates -.-
                    if (bank.Index == i)
                    {
                        for (int s = 0; s < bank.Samples.Capacity; s++)
                        {
                            fs.Position = offset + ((s * bankDetail.SampleSize) + bankDetail.HeaderSize);

                            var sampleInfo = fs.Read<SoundSampleInfo>(bankDetail.SampleSize);
                            sampleInfo.Offset += bankDetail.DataOffset;

                            var sample = new SoundSample() {
                                FileName = $"{s:D2}.wav",

                                NumChannels = ((sampleInfo.Flags & bankDetail.SampleChannelFlags) != 0) ? 2 : 1,
                                SampleRate = sampleInfo.SampleRate,

                                Flags = (sampleInfo.Flags & ~bankDetail.SampleChannelFlags),

                                Unknown1 = sampleInfo.Unk_0B,
                                Unknown2 = sampleInfo.Unk_0C,
                            };
                            

                            bank.Samples.Add(sample);

                            // retrieve the buffer
                            var buffer = new byte[sampleInfo.Size];

                            fs.Position = sampleInfo.Offset;
                            fs.Read(buffer, 0, buffer.Length);

                            // make sure we apply it
                            sample.Buffer = buffer;
                        }
                    }
                    
                    Banks.Add(bank);
                }
            }
        }
        
        public void SaveBinary(string filename)
        {
            using (var fs = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                var nBanks = Banks.Count;
                var header = new GSDHeader(Type, nBanks);

                fs.Write(header);
                
                var bankOffsets = new int[nBanks];
                
                var dataSize = header.FileSizeOffset;
                
                for (int i = 0; i < nBanks; i++)
                {
                    var bank = Banks[i];
                    
                    if (bank.Index != i)
                    {
                        if (Type != GSDFormatType.BK31)
                            throw new InvalidOperationException("Driv3r GSD's don't support referenced SoundBank's!");

                        bankOffsets[i] = (bank.IsNull) ? -1 : 0;
                    }
                    else
                    {
                        dataSize = Memory.Align(dataSize, 2048);

                        var bankOffset = dataSize;
                        bankOffsets[i] = bankOffset;
                        
                        var sndBank = header.CreateSoundBank(Type, bank);
                        var samples = bank.Samples;
                        var nSamples = samples.Count;

                        var sampleDataOffset = Memory.Align((sndBank.HeaderSize + (nSamples * sndBank.SampleSize)), 64);
                        var sampleDataSize = 0;

                        var sampleOffsets = new int[nSamples];
                        var sampleSizes = new int[nSamples];

                        for (int s = 0; s < nSamples; s++)
                        {
                            var sample = samples[s];

                            var sampleOffset = sampleDataSize;
                            var sampleSize = sample.Buffer.Length;

                            sampleOffsets[s] = sampleOffset;
                            sampleSizes[s] = sampleSize;

                            sampleDataSize += Memory.Align(sampleSize, 4);
                        }
                        
                        sampleDataSize = Memory.Align(sampleDataSize, 4);
                        sndBank.SetDataInfo(sampleDataOffset, sampleDataSize);

                        var sndBankData = Memory.Copy(sndBank, sndBank.HeaderSize);

                        fs.Position = dataSize;
                        fs.Write(sndBankData);

                        for (int s = 0; s < nSamples; s++)
                        {
                            fs.Position = bankOffset + (sndBank.HeaderSize + (s * sndBank.SampleSize));

                            var sample = samples[s];

                            var sampleInfo = new SoundSampleInfo() {
                                Offset = sampleOffsets[s],
                                Size = sampleSizes[s],
                                SampleRate = (ushort)sample.SampleRate,
                                Flags = (byte)sample.Flags,
                                Unk_0B = (byte)sample.Unknown1,
                                Unk_0C = sample.Unknown2,
                            };

                            if (sample.NumChannels == 2)
                                sampleInfo.Flags |= (byte)sndBank.SampleChannelFlags;

                            var sampleData = new byte[sndBank.SampleSize];

                            Memory.Fill(MagicNumber.FIREBIRD, sampleData); // ;)
                            Memory.Copy(sampleInfo, sampleData, SoundSampleInfo.SizeOf);

                            fs.Write(sampleData);

                            fs.Position = bankOffset + (sampleDataOffset + sampleInfo.Offset);
                            fs.Write(sample.Buffer);
                        }

                        dataSize += (sampleDataOffset + sampleDataSize);
                    }
                }

                dataSize = Memory.Align(dataSize, header.Alignment);
                fs.SetLength(dataSize);

                // write bank offsets
                for (int i = 0; i < nBanks; i++)
                {
                    var bank = Banks[i];
                    var bankOffset = bankOffsets[i];
                    
                    if (bankOffset == 0)
                        bankOffset = bankOffsets[bank.Index];
                    if (bankOffset == -1)
                        bankOffset = (int)fs.Length; // force to end of file

                    fs.Position = header.ListOffset + (i * 4);
                    fs.Write(bankOffset);
                }

                // write file size
                fs.Position = header.FileSizeOffset;
                fs.Write(dataSize);
            }
        }
    }
}
