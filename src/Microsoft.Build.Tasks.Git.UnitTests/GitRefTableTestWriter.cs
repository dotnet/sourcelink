// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Hashing;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Tasks.Git.UnitTests;

internal class GitRefTableTestWriter
{
    public MemoryStream Stream { get; } = new();

    public long Position
    {
        get => Stream.Position;
        set => Stream.Position = value;
    }

    public byte[] ToArray()
        => Stream.ToArray();

    public void WriteBytes(byte[] data)
        => Stream.Write(data, 0, data.Length);

    public void WriteUInt16BE(int value)
    {
        Stream.WriteByte((byte)((value >> 8) & 0xFF));
        Stream.WriteByte((byte)(value & 0xFF));
    }

    public void WriteUInt24BE(int value)
    {
        Stream.WriteByte((byte)((value >> 16) & 0xFF));
        Stream.WriteByte((byte)((value >> 8) & 0xFF));
        Stream.WriteByte((byte)(value & 0xFF));
    }

    public void WriteUInt32BE(uint value)
    {
        Stream.WriteByte((byte)((value >> 24) & 0xFF));
        Stream.WriteByte((byte)((value >> 16) & 0xFF));
        Stream.WriteByte((byte)((value >> 8) & 0xFF));
        Stream.WriteByte((byte)(value & 0xFF));
    }

    public void WriteUInt64BE(ulong value)
    {
        Stream.WriteByte((byte)((value >> 56) & 0xFF));
        Stream.WriteByte((byte)((value >> 48) & 0xFF));
        Stream.WriteByte((byte)((value >> 40) & 0xFF));
        Stream.WriteByte((byte)((value >> 32) & 0xFF));
        Stream.WriteByte((byte)((value >> 24) & 0xFF));
        Stream.WriteByte((byte)((value >> 16) & 0xFF));
        Stream.WriteByte((byte)((value >> 8) & 0xFF));
        Stream.WriteByte((byte)(value & 0xFF));
    }

    public void WriteVarInt(int value)
    {
        const int V2 = 1 << 7;
        const int V3 = V2 + (1 << 14);
        const int V4 = V3 + (1 << 21);
        const int V5 = V4 + (1 << 28);

        int shift;
        if (value >= V5)
        {
            value -= V5;
            shift = 28;
        }
        else if (value >= V4)
        {
            value -= V4;
            shift = 21;
        }
        else if (value >= V3)
        {
            value -= V3;
            shift = 14;
        }
        else if (value >= V2)
        {
            value -= V2;
            shift = 7;
        }
        else
        {
            shift = 0;
        }

        for (var s = shift; s >= 0; s -= 7)
        {
            Stream.WriteByte((byte)((value >> s) & 0x7f | (s > 0 ? 0x80 : 0)));
        }
    }

    public void WriteFooter(
        Action<GitRefTableTestWriter> writeHeader,
        ulong refIndexPosition,
        ulong objPosition,
        ulong objIndexPosition,
        ulong logPosition,
        ulong logIndexPosition)
    {
        var startPosition = (int)Stream.Position;
        writeHeader(this);
        WriteUInt64BE(refIndexPosition);
        WriteUInt64BE(objPosition);
        WriteUInt64BE(objIndexPosition);
        WriteUInt64BE(logPosition);
        WriteUInt64BE(logIndexPosition);
        var endPosition = (int)Stream.Position;

        WriteUInt32BE(Crc32.HashToUInt32(Stream.ToArray().AsSpan()[startPosition..endPosition]));
    }

    public void WriteFooter(GitRefTableReader.Header header, long refIndexPosition)
    {
        WriteFooter(
            writer => writer.WriteHeader(header),
            (ulong)refIndexPosition,
            objPosition: 0,
            objIndexPosition: 0,
            logPosition: 0,
            logIndexPosition: 0);
    }

    public void WriteHeader(
        uint magic,
        byte version,
        int blockSize,
        ulong minUpdate,
        ulong maxUpdate,
        string? hashId = null)
    {
        WriteUInt32BE(magic);
        Stream.WriteByte(version);
        WriteUInt24BE(blockSize);
        WriteUInt64BE(minUpdate);
        WriteUInt64BE(maxUpdate);
        if (hashId != null)
        {
            WriteBytes(Encoding.ASCII.GetBytes(hashId));
        }
    }

    public void WriteHeader(GitRefTableReader.Header header)
    {
        WriteHeader(
            magic: 0x52454654, // 'RFTB'
            version: 1,
            blockSize: header.BlockSize,
            minUpdate: 0,
            maxUpdate: 0);
    }

    public void WriteNameAndValueType(
        int prefixLength,
        int suffixLength,
        byte valueType,
        byte[] suffix)
    {
        WriteVarInt(prefixLength);
        WriteVarInt((suffixLength << 3) | valueType);
        WriteBytes(suffix);
    }

    public void WriteRefRecord(
        int prefixLength,
        byte valueType,
        byte[] suffix,
        int updateIndexDelta,
        byte[]? objectName = null,
        byte[]? peeledObjectName = null,
        byte[]? symbolicRef = null)
    {
        WriteNameAndValueType(
            prefixLength,
            suffix.Length,
            valueType,
            suffix);

        WriteVarInt(updateIndexDelta);

        if (objectName != null)
        {
            WriteBytes(objectName);
        }

        if (peeledObjectName != null)
        {
            WriteBytes(peeledObjectName);
        }

        if (symbolicRef != null)
        {
            WriteVarInt(symbolicRef.Length);
            WriteBytes(symbolicRef);
        }
    }

    public void WriteRefIndexRecord(
        int prefixLength,
        byte valueType,
        byte[] suffix,
        long blockPosition)
    {
        WriteNameAndValueType(
            prefixLength,
            suffix.Length,
            valueType,
            suffix);

        WriteVarInt((int)blockPosition);
    }

    public void WriteRestartOffsets(params int[] offsets)
    {
        foreach (var offset in offsets)
        {
            WriteUInt24BE(offset);
        }

        WriteUInt16BE(offsets.Length);
    }

    public long WriteBlock(GitRefTableReader.Header? header, char kind, Action<GitRefTableTestWriter, long> writeContent)
    {
        var blockStart = Stream.Position;

        if (header != null)
        {
            WriteHeader(
                magic: 0x52454654, // 'RFTB'
                version: 1,
                blockSize: header.Value.BlockSize,
                minUpdate: 0,
                maxUpdate: 0);
        }

        var blockTypePosition = Stream.Position;

        Stream.WriteByte((byte)kind);

        // uint24(block_len)
        var lengthPosition = Stream.Position;
        WriteUInt24BE(0);

        writeContent(this, blockStart);

        var blockEnd = Stream.Position;

        // patch length:
        Stream.Position = lengthPosition;
        WriteUInt24BE((int)(blockEnd - blockStart));

        Stream.Position = blockEnd;

        return blockTypePosition;
    }

    public static byte[] GetObjectName(byte b0, ObjectNameFormat format = ObjectNameFormat.Sha1)
    {
        var name = new byte[format.HashSize];
        name[0] = b0;
        return name;
    }

    public readonly record struct NameOrSymRef(byte[]? ObjectName, byte[]? SymbolicReference)
    {
        public static implicit operator NameOrSymRef(string symbolicReference) => new(null, Encoding.UTF8.GetBytes(symbolicReference));
        public static implicit operator NameOrSymRef(byte objectName) => new(GetObjectName(objectName), null);
    }

    public long WriteRefBlock(
        GitRefTableReader.Header? header,
        params (string prefix, string suffix, NameOrSymRef name)[] records)
    {
        return WriteBlock(header, 'r', (writer, blockStart) =>
        {
            var offsets = new List<int>();

            foreach (var (prefix, suffix, name) in records)
            {
                if (prefix == "")
                {
                    offsets.Add((int)(writer.Stream.Position - blockStart));
                }

                writer.WriteRefRecord(
                    prefixLength: prefix.Length,
                    valueType: (byte)(name.ObjectName != null ? 1 : 3),
                    suffix: Encoding.ASCII.GetBytes(suffix),
                    updateIndexDelta: 0,
                    objectName: name.ObjectName,
                    symbolicRef: name.SymbolicReference);
            }

            WriteRestartOffsets([.. offsets]);
        });
    }

    public long WriteRefIndexBlock(params (string prefix, string suffix, long blockPosition)[] records)
    {
        return WriteBlock(header: null, 'i', (writer, blockStart) =>
        {
            var offsets = new List<int>();

            foreach (var (prefix, suffix, blockPosition) in records)
            {
                if (prefix == "")
                {
                    offsets.Add((int)(Stream.Position - blockStart));
                }

                writer.WriteRefIndexRecord(
                    prefixLength: prefix.Length,
                    valueType: 0,
                    suffix: Encoding.ASCII.GetBytes(suffix),
                    blockPosition: blockPosition);
            }

            WriteRestartOffsets([.. offsets]);
        });
    }

    public void WritePadding(int alignment)
    {
        if (alignment == 0)
            return;

        while (Stream.Position % alignment != 0)
        {
            Stream.WriteByte(0);
        }
    }

    public static byte[] GetRefTableBlob((string reference, NameOrSymRef name)[] references)
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 0,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        writer.WriteRefBlock(header, [.. references.Select(r => ("", r.reference, r.name))]);
        writer.WriteFooter(header, refIndexPosition: 0);

        return writer.ToArray();
    }
}
