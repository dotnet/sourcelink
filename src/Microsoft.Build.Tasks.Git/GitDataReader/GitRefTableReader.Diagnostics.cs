// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if DEBUG

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Tasks.Git;

// Utilities for diagnosing reader issues.

internal sealed partial class GitRefTableReader
{
    public record Block(
        char Type,
        long Position,
        int UnalignedSize,
        List<RefRecord> RefRecords,
        List<RefIndexRecord> IndexRecords,
        List<int> RestartOffsets)
    {
        public override string ToString()
            => $"Type: '{Type}', Position: {Position}, UnalignedSize: {UnalignedSize}, Records: {RefRecords.Count + IndexRecords.Count}, RestartOffsets: {RestartOffsets.Count}";
    }

    /// <summary>
    /// Returns all ref-blocks and ref-index-blocks.
    /// </summary>
    public List<Block> GetBlocks()
    {
        var footer = ReadHeaderAndFooter();

        var blocks = new List<Block>();

        Position = footer.Header.Size;
        var blockStartPosition = 0L;

        while (Position < footer.Position)
        {
            var blockType = ReadByte();
            var unalignedSize = ReadUInt24BE();

            var refRecords = new List<RefRecord>();
            var indexRecords = new List<RefIndexRecord>();
            var restartOffsets = new List<int>();

            if (blockType is not (BlockTypeRef or BlockTypeIndex))
            {
                break;
            }

            var firstRecordPosition = Position;

            Position = blockStartPosition + unalignedSize - sizeof(ushort);
            var restartCount = ReadUInt16BE();
            Position = firstRecordPosition;

            var endOfRecords = blockStartPosition + unalignedSize - restartCount * SizeOfUInt24 - sizeof(ushort);
            while (Position < endOfRecords)
            {
                if (blockType == BlockTypeRef)
                {
                    refRecords.Add(ReadRefRecord(footer.Header, priorName: null));
                }
                else
                {
                    indexRecords.Add(ReadRefIndexRecord(priorName: null));
                }
            }

            for (var i = 0; i < restartCount; i++)
            {
                restartOffsets.Add(ReadUInt24BE());
            }

            // restart_count
            _ = ReadUInt16BE();

            blocks.Add(new Block((char)blockType, blockStartPosition, unalignedSize, refRecords, indexRecords, restartOffsets));

            // If the file is unaligned the next block starts at the end of the current block,
            // otherwise the current block is padded to block size and the next block starts after the padding.
            blockStartPosition = footer.Header.BlockSize > 0 ? blockStartPosition + footer.Header.BlockSize : Position;

            while (Position < blockStartPosition)
            {
                var padding = ReadByte();
                if (padding != 0)
                {
                    throw new InvalidDataException($"Unexpected non-zero padding byte: 0x{padding:X2}.");
                }
            }
        }

        return blocks;
    }
}

#endif