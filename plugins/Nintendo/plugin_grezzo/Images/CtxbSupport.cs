﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Kanvas.Encoding;
using Komponent.IO.Attributes;
using Kontract.Kanvas;
using Kontract.Models.Image;

namespace plugin_grezzo.Images
{
    class CtxbHeader
    {
        [FixedLength(4)]
        public string magic = "ctxb";
        public int fileSize;
        public long chunkCount;
        public int chunkOffset;
        public int texDataOffset;
    }

    class CtxbChunk
    {
        [FixedLength(4)]
        public string magic = "tex ";
        public int chunkSize;
        public int texCount;
        [VariableLength("texCount")]
        public CtxbEntry[] textures;
    }

    class CtxbEntry
    {
        public int dataLength;
        public short unk1;
        public short unk2;
        public short width;
        public short height;
        public ushort imageFormat;
        public ushort dataType;
        public int dataOffset;
        [FixedLength(16)]
        public string name;
    }

    class CtxbImageInfo : ImageInfo
    {
        public int ChunkIndex { get; }

        public CtxbEntry Entry { get; }

        public CtxbImageInfo(byte[] imageData, int imageFormat, Size imageSize, int chunkIndex, CtxbEntry entry) : base(imageData, imageFormat, imageSize)
        {
            ChunkIndex = chunkIndex;
            Entry = entry;
        }

        public CtxbImageInfo(byte[] imageData, IList<byte[]> mipMaps, int imageFormat, Size imageSize, int chunkIndex, CtxbEntry entry) : base(imageData, mipMaps, imageFormat, imageSize)
        {
            ChunkIndex = chunkIndex;
            Entry = entry;
        }
    }

    public class CtxbSupport
    {
        private static readonly IDictionary<uint, IColorEncoding> CtxbFormats = new Dictionary<uint, IColorEncoding>
        {
            // composed of dataType and PixelFormat
            // short+short
            [0x14016752] = new Rgba(8, 8, 8, 8),
            [0x80336752] = new Rgba(4, 4, 4, 4),
            [0x80346752] = new Rgba(5, 5, 5, 1),
            [0x14016754] = new Rgba(8, 8, 8),
            [0x83636754] = new Rgba(5, 6, 5),
            [0x14016756] = new La(0, 8),
            [0x67616756] = new La(0, 4),
            [0x14016757] = new La(8, 0),
            [0x67616757] = new La(4, 0),
            [0x67606758] = new La(4, 4),
            [0x14016758] = new La(8, 8),
            [0x0000675A] = new Etc1(false, true),
            [0x0000675B] = new Etc1(true, true),
            [0x1401675A] = new Etc1(false, true),
            [0x1401675B] = new Etc1(true, true)
        };

        public static EncodingDefinition GetEncodingDefinition()
        {
            var definition = new EncodingDefinition();
            definition.AddColorEncodings(CtxbFormats.ToDictionary(x => (int)x.Key, y => y.Value));

            return definition;
        }
    }
}