// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Hashing;
using System.Text;

namespace Microsoft.Build.Tasks.Git;

/// <summary>
/// Implements reftable data structure used by Git to store references.
/// See https://git-scm.com/docs/reftable
/// </summary>
internal sealed partial class GitRefTableReader(Stream stream) : IDisposable
{
    private static readonly Encoding s_utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private const uint Magic = 'R' << 24 | 'E' << 16 | 'F' << 8 | 'T';
    private const uint HashId_SHA1 = 's' << 24 | 'h' << 16 | 'a' << 8 | '1';
    private const uint HashId_SHA256 = 's' << 24 | '2' << 16 | '5' << 8 | '6';

    private const byte BlockTypeRef = (byte)'r';
    private const byte BlockTypeIndex = (byte)'i';

    private const int MinRefBlockSize =
        1 + // block_type
        3 + // uint24(block_len)
        3 + // uint24(restart_offset)+
        2;  // uint16(restart_count)

    public void Dispose()
    {
        stream.Dispose();
    }

    /// <exception cref="IOException"/>
    /// <exception cref="EndOfStreamException"/>
    /// <exception cref="InvalidDataException"/>
    public bool TryFindReference(string referenceName, out string? objectName, out string? symbolicReference)
    {
        var record = FindRefRecord(referenceName);
        if (record == null)
        {
            objectName = null;
            symbolicReference = null;
            return false;
        }

        objectName = record.Value.ObjectName;
        symbolicReference = record.Value.SymbolicRef;
        return true;
    }

    private RefRecord? FindRefRecord(string referenceName)
    {
        Position = 0;

        // Readers are required to read header and validate version it before reading the footer.
        // https://git-scm.com/docs/reftable#_footer
        var header = ReadHeader();

        var footerPosition = Length - header.Size - Footer.SizeExcludingHeader;

        // Skip ahead to read the footer.
        // It contains a copy of header and position of the RefIndex.
        Position = footerPosition;
        var footer = ReadFooter();

        if (footer.RefIndexPosition > 0)
        {
            Position = footer.RefIndexPosition;
            if (ReadByte() != BlockTypeIndex)
            {
                throw new InvalidDataException();
            }

            return SearchRefIndex(footer.Header, referenceName);
        }

        // No RefIndex, read RefBlocks sequentially.

        var blockStartPosition = 0L;

        while (true)
        {
            var isFirstBlock = blockStartPosition == 0;

            // The first block starts with header.
            Position = blockStartPosition + (isFirstBlock ? header.Size : 0);
            switch (ReadByte())
            {
                case BlockTypeRef:
                    break;

                case BlockTypeIndex:
                    throw new InvalidDataException();

                default:
                    return isFirstBlock ? throw new InvalidDataException() : null;
            }

            var result = SearchRefBlock(footer.Header, referenceName, out var blockEndPosition);
            if (result != null)
            {
                return result;
            }

            // If the file is unaligned the next block starts at the end of the current block,
            // otherwise the current block its padded to block size and the next block starts after the padding.
            blockStartPosition = footer.Header.BlockSize > 0 ? blockStartPosition + footer.Header.BlockSize : blockEndPosition;
            if (blockStartPosition >= footerPosition)
            {
                return null;
            }
        }
    }

    internal Header ReadHeader()
    {
        // https://git-scm.com/docs/reftable#_header_version_1

        var headerStartPosition = Position;

        var magic = ReadUInt32BE();
        if (magic != Magic)
        {
            throw new InvalidDataException();
        }

        var version = ReadByte();
        if (version is not (1 or 2))
        {
            throw new InvalidDataException();
        }

        // block_size
        var blockSize = ReadUInt24BE();

        // min_update_index
        _ = ReadUInt64BE();

        // max_update_index
        _ = ReadUInt64BE();

        ObjectNameFormat objectNameFormat;
        if (version == 1)
        {
            objectNameFormat = ObjectNameFormat.Sha1;
        }
        else
        {
            objectNameFormat = ReadUInt32BE() switch
            {
                HashId_SHA1 => ObjectNameFormat.Sha1,
                HashId_SHA256 => ObjectNameFormat.Sha256,
                _ => throw new InvalidDataException(),
            };
        }

        return new Header
        {
            Size = (int)(Position - headerStartPosition),
            BlockSize = blockSize,
            ObjectNameFormat = objectNameFormat
        };
    }

    internal Footer ReadFooter()
    {
        var footerStartPosition = Position;

        // header
        var header = ReadHeader();

        // uint64(ref_index_position)
        var refIndexPosition = ReadUInt64BE();
        if (refIndexPosition > (ulong)Length)
        {
            throw new InvalidDataException();
        }

        // obj_position (not need for reference lookup):
        _ = ReadUInt64BE();

        // obj_index_position (not need for reference lookup):
        _ = ReadUInt64BE();

        // log_position (not need for reference lookup):
        _ = ReadUInt64BE();

        // log_index_position (not need for reference lookup):
        _ = ReadUInt64BE();

        var checksumedSectionEndPosition = Position;

        // uint32(CRC - 32 of above)
        var checksum = ReadUInt32BE();

        // Validate checksum:
        Position = footerStartPosition;
        var buffer = ReadBytes((int)(checksumedSectionEndPosition - footerStartPosition));
        var computedChecksum = Crc32.HashToUInt32(buffer);
        if (computedChecksum != checksum)
        {
            throw new InvalidDataException();
        }

        return new Footer
        {
            Header = header,
            RefIndexPosition = (long)refIndexPosition
        };
    }

    /// <summary>
    /// RefIndex stores the last reference name of each RefBlock.
    /// </summary>
    private RefRecord? SearchRefIndex(Header header, string referenceName)
    {
        // 'i' (already read)
        // uint24(block_len)
        // index_record+
        // uint24(restart_offset)+
        // uint16(restart_count)
        // padding?

        // The index block may exceed the block size specified in the header.
        var restartOffsets = ReadRestartOffsets(header, blockLengthLimited: false, out var blockStartPosition, out _);

        var (record, firstGreater) = restartOffsets.BinarySearch(
            index: 0,
            length: restartOffsets.Length - 1,
            selector: restartOffset =>
            {
                Position = blockStartPosition + restartOffset;
                return ReadRefIndexRecord(priorName: "");
            },
            compareItemToSearchValue: item => StringComparer.Ordinal.Compare(item.LastRefName, referenceName));

        if (firstGreater < 0)
        {
            // the last reference of the block is the one we are looking for:
            Position = record.BlockPosition;
            return SearchBlock(header, referenceName);
        }

        // firstGreater points to the first record at a restart offset that contains references with last name larger than the searched value.
        // The reference is either in the record at firstGreater, or in the previous run.

        if (firstGreater < restartOffsets.Length - 1)
        {
            Position = blockStartPosition + restartOffsets[firstGreater];
            record = ReadRefIndexRecord(priorName: "");

            Debug.Assert(StringComparer.Ordinal.Compare(referenceName, record.LastRefName) < 0);

            Position = record.BlockPosition;
            var result = SearchBlock(header, referenceName);
            if (result != null)
            {
                return result;
            }
        }

        firstGreater--;

        if (firstGreater == -1)
        {
            // reference is not found - it would be ordered before the first record
            return null;
        }

        Position = blockStartPosition + restartOffsets[firstGreater];
        var endPosition = blockStartPosition + restartOffsets[firstGreater + 1];

        var priorName = "";
        while (Position < endPosition)
        {
            record = ReadRefIndexRecord(priorName);

            if (StringComparer.Ordinal.Compare(referenceName, record.LastRefName) <= 0)
            {
                // the last reference of the block is the one we are looking for:
                Position = record.BlockPosition;
                return SearchBlock(header, referenceName);
            }

            priorName = record.LastRefName;
        }

        return null;
    }

    public RefRecord? SearchBlock(Header header, string referenceName)
    {
        return ReadByte() switch
        {
            BlockTypeRef => SearchRefBlock(header, referenceName, out _),
            BlockTypeIndex => SearchRefIndex(header, referenceName),
            _ => throw new InvalidDataException(),
        };
    }

    private RefRecord? SearchRefBlock(Header header, string referenceName, out long blockEndPosition)
    {
        // 'r' (already read)
        // uint24(block_len)
        // ref_record+
        // uint24(restart_offset)+
        // uint16(restart_count)
        // padding?

        var restartOffsets = ReadRestartOffsets(header, blockLengthLimited: true, out var blockStartPosition, out blockEndPosition);

        var (record, firstGreater) = restartOffsets.BinarySearch(
            index: 0,
            length: restartOffsets.Length - 1,
            selector: restartOffset =>
            {
                Position = blockStartPosition + restartOffset;
                return ReadRefRecord(header, priorName: "");
            },
            compareItemToSearchValue: item => StringComparer.Ordinal.Compare(item.RefName, referenceName));

        if (firstGreater < 0)
        {
            // The record at the index has the reference we are looking for.
            return record;
        }

        // firstGreater points to the first record at a restart offset that has reference name greater than the searched value.
        // Record runs are sorted by *first* reference name of the run (run starts at a restart offset and ends at the next restart offset).
        // Hence, firstGreater - 1 points to the run that contains the reference we are looking for.
        if (firstGreater == 0)
        {
            // reference is not found - it would be ordered before the first record
            return null;
        }

        Position = blockStartPosition + restartOffsets[firstGreater - 1];
        var endPosition = blockStartPosition + restartOffsets[firstGreater];

        var priorName = "";
        while (Position < endPosition)
        {
            record = ReadRefRecord(header, priorName);

            if (record.RefName == referenceName)
            {
                return record;
            }

            priorName = record.RefName;
        }

        return null;
    }

    /// <summary>
    /// Returns offsets (relative to the start of the block).
    /// Each offset points at a record with no prefix optionally followed by records that use prefix compression.
    /// The last offset points at the end of records in the block.
    /// Offset values are increasing.
    /// </summary>
    internal int[] ReadRestartOffsets(Header header, bool blockLengthLimited, out long blockStartPosition, out long blockEndPosition)
    {
        const int SizeOfBlockType = sizeof(byte);
        const int SizeOfRestartCount = sizeof(ushort);
        const int SizeOfRestartOffset = SizeOfUInt24;
        const int SizeOfBlockLength = SizeOfUInt24;

        // The first block includes the header.
        // block_type is already read.
        var isFirstBlock = Position == header.Size + SizeOfBlockType;

        // Block starts with a block_type:
        blockStartPosition = isFirstBlock ? 0 : Position - SizeOfBlockType;

        // block_len of the first block includes the file header.
        // Must be less than or equal to block size, unless the file is unaligned.
        var blockLength = ReadUInt24BE();
        if (blockLengthLimited && header.BlockSize > 0 && blockLength > header.BlockSize ||
            blockLength < MinRefBlockSize)
        {
            throw new InvalidDataException();
        }

        // block_len excludes padding:
        blockEndPosition = blockStartPosition + blockLength;

        // uint16(restart_count)
        Position = blockStartPosition + blockLength - SizeOfRestartCount;
        var restartCount = ReadUInt16BE();
        if (restartCount == 0)
        {
            throw new InvalidDataException();
        }

        // uint24(restart_offset)+
        var endOffset = blockLength - SizeOfRestartCount - SizeOfRestartOffset * restartCount;
        Position = blockStartPosition + endOffset;

        int[] offsets;
        try
        {
            offsets = new int[restartCount + 1];
        }
        catch (OutOfMemoryException)
        {
            throw new InvalidDataException();
        }

        for (var i = 0; i < restartCount; i++)
        {
            // Offset relative to the start of the block:
            var offset = ReadUInt24BE();

            // first offset points to the first record:
            if (i == 0 && offset != (isFirstBlock ? header.Size : 0) + SizeOfBlockType + SizeOfBlockLength)
            {
                throw new InvalidDataException();
            }

            // offsets must be increasing:
            if (i > 0 && offset <= offsets[i - 1])
            {
                throw new InvalidDataException();
            }

            if (offset >= endOffset)
            {
                throw new InvalidDataException();
            }

            offsets[i] = offset;
        }

        offsets[^1] = endOffset;

        return offsets;
    }

    private (string name, byte valueType) ReadNameAndValueType(string priorName)
    {
        // varint(prefix_length)
        var prefixLength = ReadVarInt();

        // varint((suffix_length << 3) | value_type)
        var suffixLengthAndValueType = ReadVarInt();
        var suffixLength = suffixLengthAndValueType >> 3;
        var valueType = (byte)(suffixLengthAndValueType & 0x07);

        // suffix
        var suffixBytes = ReadBytes(suffixLength);
        try
        {
            var name = priorName[0..prefixLength] + s_utf8.GetString(suffixBytes);
            return (name, valueType);
        }
        catch
        {
            throw new InvalidDataException();
        }
    }

    internal RefIndexRecord ReadRefIndexRecord(string priorName)
    {
        var (name, valueType) = ReadNameAndValueType(priorName);
        if (valueType != 0)
        {
            throw new InvalidDataException();
        }

        // position of RefBlock from the start of the file:
        var blockPosition = ReadVarInt();

        return new RefIndexRecord
        {
            LastRefName = name,
            BlockPosition = blockPosition
        };
    }

    internal RefRecord ReadRefRecord(Header header, string priorName)
    {
        var (name, valueType) = ReadNameAndValueType(priorName);

        // varint(update_index_delta)
        _ = ReadVarInt();

        string? symbolicRef = null;
        string? objectName = null;

        var objectIdSize = header.ObjectNameFormat.HashSize;

        switch (valueType)
        {
            case 0: // deletion, no value -- stops lookup from opening previous reftable file
                break;

            case 1: // object name
                objectName = ReadObjectName(objectIdSize);
                break;

            case 2: // value, peeled target
                objectName = ReadObjectName(objectIdSize);

                // peeled target object name
                _ = ReadObjectName(objectIdSize);
                break;

            case 3: // symbolic ref
                symbolicRef = ReadSymbolicRef();
                break;

            default:
                throw new InvalidDataException();
        }

        return new RefRecord
        {
            RefName = name,
            ObjectName = objectName,
            SymbolicRef = symbolicRef
        };
    }

    private string ReadSymbolicRef()
    {
        var length = ReadVarInt();
        var bytes = ReadBytes(length);

        try
        {
            return s_utf8.GetString(bytes);
        }
        catch
        {
            throw new InvalidDataException();
        }
    }
}
