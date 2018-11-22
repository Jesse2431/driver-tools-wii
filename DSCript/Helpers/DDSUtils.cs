﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace DSCript
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    public struct DDSPixelFormat
    {
        public int Size;
        public int Flags;
        public int FourCC;
        public int RGBBitCount;
        public int RBitMask;
        public int GBitMask;
        public int BBitMask;
        public int ABitMask;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x7C)]
    public unsafe struct DDSHeader
    {
        public static readonly int SizeOf = Marshal.SizeOf(typeof(DDSHeader));

        public int Size;
        public int Flags;
        public int Height;
        public int Width;
        public int PitchOrLinearSize;
        public int Depth;
        public int MipMapCount;

        private fixed int Reserved1[11];

        public DDSPixelFormat PixelFormat;

        public int Caps;
        public int Caps2;
        public int Caps3;
        public int Caps4;

        private int Reserved2;
    }

    public static class DDSUtils
    {
        // ugly pink 8x8 texture (DXT1)
        private static readonly byte[] NullTex = {
            0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00, 0x07, 0x10, 0x08, 0x00,
            0x08, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00,
            0x44, 0x58, 0x54, 0x31, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x18, 0xF8, 0x17, 0xF8,
            0xFF, 0xFF, 0xFF, 0xFF, 0x18, 0xF8, 0x17, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF,
            0x18, 0xF8, 0x17, 0xF8, 0xFF, 0xFF, 0xFF, 0xFF, 0x18, 0xF8, 0x17, 0xF8,
            0xFF, 0xFF, 0xFF, 0xFF
        };

        private static readonly byte[] RGBHeader = {
            0x44, 0x44, 0x53, 0x20, 0x7C, 0x00, 0x00, 0x00, 0x07, 0x10, 0x08, 0x00,
            0x08, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x41, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF, 0x00,
            0x00, 0xFF, 0x00, 0x00, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF,
            0x00, 0x10, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        public static byte[] GetNullTex()
        {
            var result = new byte[NullTex.Length];
            Buffer.BlockCopy(NullTex, 0, result, 0, result.Length);

            return result;
        }

        public static byte[] MakeRGBATexture(Vector4 color)
        {
            var pixels = new byte[0x100];

            var r = (byte)(color.X * 255.999f);
            var g = (byte)(color.Y * 255.999f);
            var b = (byte)(color.Z * 255.999f);
            var a = (byte)(color.W * 255.999f);

            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i + 0] = b;
                pixels[i + 1] = g;
                pixels[i + 2] = r;
                pixels[i + 3] = a;
            }

            var result = new byte[RGBHeader.Length + pixels.Length];

            Buffer.BlockCopy(RGBHeader, 0, result, 0, RGBHeader.Length);
            Buffer.BlockCopy(pixels, 0, result, RGBHeader.Length, pixels.Length);

            return result;
        }

        public static unsafe bool GetHeaderInfo(Stream stream, ref DDSHeader header)
        {
            if (stream.ReadInt32() == 0x20534444)
            {
                var length = DDSHeader.SizeOf;

                var data = new byte[length];
                var ptr = __makeref(header);

                stream.Read(data, 0, length);
                Marshal.Copy(data, 0, *(IntPtr*)&ptr, length);

                return true;
            }

            // not a valid DDS texture
            return false;
        }

        public static bool GetHeaderInfo(byte[] buffer, ref DDSHeader header)
        {
            using (var ms = new MemoryStream(buffer))
                return GetHeaderInfo(ms, ref header);
        }

        public static int GetTextureType(int fourCC)
        {
            switch (fourCC)
            {
            case 0x31545844: return 1;
            case 0x32545844: return 2;
            case 0x33545844: return 3;
            case 0x35545844: return 5;
            // RGB[:A] (non-spec)
            case 0:
                return 128;
            default:
                return 0;
            }
        }
    }
}
