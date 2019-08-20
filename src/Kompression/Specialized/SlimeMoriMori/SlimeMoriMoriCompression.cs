﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kompression.Huffman.Support;
using Kompression.IO;
using Kompression.LempelZiv;
using Kompression.LempelZiv.MatchFinder;
using Kompression.LempelZiv.Parser;
using Kompression.Specialized.SlimeMoriMori.Decoders;
using Kompression.Specialized.SlimeMoriMori.Deobfuscators;
using Kompression.Specialized.SlimeMoriMori.Encoders;
using Kompression.Specialized.SlimeMoriMori.Obfuscators;
using Kompression.Specialized.SlimeMoriMori.ValueReaders;
using Kompression.Specialized.SlimeMoriMori.ValueWriters;

namespace Kompression.Specialized.SlimeMoriMori
{
    /// <summary>
    /// The <see cref="ICompression"/> for the compression in Slime Mori Mori for the GBA.
    /// </summary>
    public class SlimeMoriMoriCompression : ICompression
    {
        private readonly int _huffmanMode;
        private readonly int _compressionMode;
        private readonly int _obfuscationMode;
        private readonly bool _isCompressable;

        public SlimeMoriMoriCompression()
        {
            _isCompressable = false;
        }

        public SlimeMoriMoriCompression(int huffmanMode, int compressionMode, int obfuscationMode = 0)
        {
            _huffmanMode = huffmanMode;
            _compressionMode = compressionMode;
            _obfuscationMode = obfuscationMode;
            _isCompressable = true;
        }

        public void Decompress(Stream input, Stream output)
        {
            var originalInputPosition = input.Position;
            if (input.ReadByte() != 0x70)
                return;

            input.Position = 7;
            var identByte = input.ReadByte();

            var valueReader = CreateValueReader((identByte >> 3) & 0x3);
            var decoder = CreateDecoder(identByte & 0x7, valueReader);

            input.Position = originalInputPosition;
            decoder.Decode(input, output);

            var deobfuscator = CreateDeobfuscator((identByte >> 5) & 0x7);
            if (deobfuscator != null)
            {
                output.Position = 0;
                deobfuscator.Deobfuscate(output);
            }
        }

        public void Compress(Stream input, Stream output)
        {
            if (!_isCompressable)
                throw new InvalidOperationException("This instance doesn't allow for compression.");

            var inputArray = ToArray(input);

            // Obfuscate the data
            var obfuscator = CreateObfuscator(_obfuscationMode);
            if (obfuscator != null)
            {
                output.Position = 0;
                obfuscator.Obfuscate(inputArray);
                output.Position = 0;
            }

            // Find all Lz matches
            var matches = FindMatches(inputArray);

            // Create huffman tree and value writer based on match filtered values
            var huffmanInput = RemoveMatchesFromInput(inputArray, matches);
            var tree = CreateHuffmanTree(huffmanInput, _huffmanMode);
            var valueWriter = CreateValueWriter(_huffmanMode, tree);

            using (var bw = new BitWriter(output, BitOrder.MSBFirst, 4, ByteOrder.LittleEndian))
            {
                // Write header data
                bw.WriteBits((int)input.Length, 0x18);
                bw.WriteByte(0x70);

                var identByte = _compressionMode & 0x7;
                identByte |= (_huffmanMode & 0x3) << 3;
                identByte |= (_obfuscationMode & 0x7) << 5;
                bw.WriteByte(identByte);

                // Write huffman tree
                WriteHuffmanTree(bw, tree, _huffmanMode);

                // Encode the input data
                var encoder = CreateEncoder(_compressionMode, valueWriter);
                encoder.Encode(input, bw, matches);

                // Flush the buffer
                bw.Flush();
            }
        }

        private LzMatch[] FindMatches(byte[] input)
        {
            // Optimal parse all LZ matches
            var parser = new OptimalParser(
                new NeedleHaystackMatchFinder(3, input.Length, input.Length, 1),
                new SlimePriceCalculator(_compressionMode, _huffmanMode));

            return parser.Parse(input, 0);
        }

        private void WriteHuffmanTree(BitWriter bw, HuffmanTreeNode rootNode, int huffmanMode)
        {
            int bitDepth;
            switch (huffmanMode)
            {
                case 1:
                    bitDepth = 4;
                    break;
                case 2:
                    bitDepth = 8;
                    break;
                default:
                    return;
            }

            IList<HuffmanTreeNode> depthList = rootNode.Children;
            for (var i = 0; i < 16; i++)
            {
                var valuesWithBitCount = depthList.Count(x => x.IsLeaf);
                bw.WriteByte(valuesWithBitCount);

                foreach (var value in depthList.Where(x => x.IsLeaf).Select(x => x.Code))
                {
                    bw.WriteBits(value, bitDepth);
                }
                depthList = depthList.Where(x => !x.IsLeaf).SelectMany(x => x.Children).ToList();
            }
        }

        private byte[] RemoveMatchesFromInput(byte[] input, LzMatch[] matches)
        {
            var huffmanInput = new byte[input.Length - matches.Sum(x => x.Length)];

            var huffmanInputPosition = 0;
            var inputArrayPosition = 0;
            foreach (var match in matches)
            {
                for (var i = inputArrayPosition; i < match.Position; i++)
                    huffmanInput[huffmanInputPosition++] = input[i];

                inputArrayPosition += (int)match.Position - inputArrayPosition;
                inputArrayPosition += match.Length;
            }

            for (var i = inputArrayPosition; i < input.Length; i++)
                huffmanInput[huffmanInputPosition++] = input[i];

            return huffmanInput;
        }

        #region Create methods

        private IValueReader CreateValueReader(int huffmanMode)
        {
            switch (huffmanMode)
            {
                case 1:
                    return new HuffmanReader(4);
                case 2:
                    return new HuffmanReader(8);
                default:
                    return new DefaultValueReader();
            }
        }

        private ISlimeDecoder CreateDecoder(int decompMode, IValueReader huffmanReader)
        {
            switch (decompMode)
            {
                case 1:
                    return new SlimeMode1Decoder(huffmanReader);
                case 2:
                    return new SlimeMode2Decoder(huffmanReader);
                case 3:
                    return new SlimeMode3Decoder(huffmanReader);
                case 4:
                    return new SlimeMode4Decoder(huffmanReader);
                default:
                    return new SlimeMode5Decoder(huffmanReader);
            }
        }

        private ISlimeDeobfuscator CreateDeobfuscator(int deobfuscateMode)
        {
            switch (deobfuscateMode)
            {
                case 1:
                    return new SlimeMode1Deobfuscator();
                case 2:
                    return new SlimeMode2Deobfuscator();
                case 3:
                    return new SlimeMode3Deobfuscator();
                case 4:
                    return new SlimeMode4Deobfuscator();
            }

            return null;
        }

        private HuffmanTreeNode CreateHuffmanTree(byte[] input, int huffmanMode)
        {
            switch (huffmanMode)
            {
                case 1:
                    var tree = new HuffmanTree(4);
                    var rootNode = tree.Build(input);
                    SortHuffmanTree(rootNode);
                    return rootNode;
                case 2:
                    tree = new HuffmanTree(8);
                    rootNode = tree.Build(input);
                    SortHuffmanTree(rootNode);
                    return rootNode;
                default:
                    return null;
            }
        }

        private IValueWriter CreateValueWriter(int huffmanMode, HuffmanTreeNode rootNode)
        {
            switch (huffmanMode)
            {
                case 1:
                case 2:
                    return new HuffmanWriter(rootNode);
                default:
                    return new DefaultValueWriter();
            }
        }

        private void SortHuffmanTree(HuffmanTreeNode rootNode)
        {
            var treeDepth = rootNode.GetDepth();

            IList<HuffmanTreeNode> previousDepthList = new List<HuffmanTreeNode> { rootNode };
            IList<HuffmanTreeNode> depthList = rootNode.Children;
            for (int i = 0; i < treeDepth; i++)
            {
                if (depthList.All(x => !x.IsLeaf))
                {
                    previousDepthList = depthList;
                    depthList = previousDepthList.SelectMany(x => x.Children).ToList();
                    continue;
                }

                var ordered = depthList.OrderBy(x => !x.IsLeaf).ToList();
                for (var j = 0; j < ordered.Count; j++)
                {
                    previousDepthList[j / 2].Frequency -= previousDepthList[j / 2].Children[j % 2].Frequency;
                    previousDepthList[j / 2].Children[j % 2] = ordered[j];
                    previousDepthList[j / 2].Frequency += ordered[j].Frequency;
                }

                previousDepthList = ordered.Where(x => !x.IsLeaf).ToList();
                depthList = previousDepthList.SelectMany(x => x.Children).ToList();
            }
        }

        private ISlimeEncoder CreateEncoder(int compressionMode, IValueWriter valueWriter)
        {
            // TODO: Implement all encoders
            // TODO: Create and write displacement table
            switch (compressionMode)
            {
                case 1:
                    return new SlimeMode1Encoder(valueWriter);
                case 2:
                    return new SlimeMode2Encoder(valueWriter);
                case 3:
                    return new SlimeMode3Encoder(valueWriter);
                case 4:
                    return new SlimeMode4Encoder(valueWriter);
                default:
                    return new SlimeMode5Encoder(valueWriter);
            }
        }

        private ISlimeObfuscator CreateObfuscator(int obfuscationMode)
        {
            switch (obfuscationMode)
            {
                case 1:
                    return new SlimeMode1Obfuscator();
                case 2:
                    return new SlimeMode2Obfuscator();
                case 3:
                    return new SlimeMode3Obfuscator();
                case 4:
                    return new SlimeMode4Obfuscator();
            }

            return null;
        }

        #endregion

        /// <summary>
        /// Converts a stream to an array.
        /// </summary>
        /// <param name="input">The input stream.</param>
        /// <returns>The converted array.</returns>
        private byte[] ToArray(Stream input)
        {
            var bkPos = input.Position;
            var inputArray = new byte[input.Length];

            input.Read(inputArray, 0, inputArray.Length);

            input.Position = bkPos;
            return inputArray;
        }
    }
}