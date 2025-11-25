// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text;

namespace Microsoft.Build.Tasks.Git;

internal sealed partial class GitRefTableReader
{
    private const int SizeOfUInt24 = 3;

    private readonly byte[] _buffer = new byte[64];

    private long Position
    {
        get => stream.Position;
        set => stream.Position = value;
    }

    private long Length
        => stream.Length;

    internal byte ReadByte()
    {
        var result = stream.ReadByte();
        if (result == -1)
        {
            throw new EndOfStreamException();
        }

        return (byte)result;
    }

    /// <summary>
    /// See https://git-scm.com/docs/reftable#_varint_encoding
    /// and "offset encoding" in https://git-scm.com/docs/pack-format#_original_version_1_pack_idx_files_have_the_following_format
    /// </summary>
    internal int ReadVarInt()
    {
        long result = -1;

        while (true)
        {
            var b = ReadByte();

            result = (result + 1) << 7 | (long)(b & 0x7f);
            if (result > int.MaxValue)
            {
                throw new InvalidDataException();
            }

            if ((b & 0x80) == 0)
            {
                return (int)result;
            }
        }
    }

    internal ushort ReadUInt16BE()
    {
        ReadExactly(_buffer, sizeof(ushort));
        return (ushort)(_buffer[0] << 8 | _buffer[1]);
    }

    internal int ReadUInt24BE()
    {
        ReadExactly(_buffer, 3);
        return _buffer[0] << 16 | _buffer[1] << 8 | _buffer[2];
    }

    internal uint ReadUInt32BE()
    {
        ReadExactly(_buffer, sizeof(uint));
        return
            (uint)_buffer[0] << 24 |
            (uint)_buffer[1] << 16 |
            (uint)_buffer[2] << 8 |
            _buffer[3];
    }

    internal ulong ReadUInt64BE()
    {
        ReadExactly(_buffer, sizeof(ulong));
        return
            (ulong)_buffer[0] << 56 |
            (ulong)_buffer[1] << 48 |
            (ulong)_buffer[2] << 40 |
            (ulong)_buffer[3] << 32 |
            (ulong)_buffer[4] << 24 |
            (ulong)_buffer[5] << 16 |
            (ulong)_buffer[6] << 8 |
            _buffer[7];
    }

    internal string ReadObjectName(int objectIdSize)
    {
        ReadExactly(_buffer, objectIdSize);

        var builder = new StringBuilder(objectIdSize * 2);

        for (var i = 0; i < objectIdSize; i++)
        {
            var b = _buffer[i];
            builder.Append(CharUtils.ToHexDigit((byte)(b >> 4)));
            builder.Append(CharUtils.ToHexDigit((byte)(b & 0xf)));
        }

        return builder.ToString();
    }

    internal byte[] ReadBytes(int count)
    {
        byte[] bytes;
        try
        {
            bytes = new byte[count];
        }
        catch (OutOfMemoryException)
        {
            throw new InvalidDataException();
        }

        ReadExactly(bytes);
        return bytes;
    }

    internal void ReadExactly(byte[] buffer)
        => ReadExactly(buffer, buffer.Length);

    internal void ReadExactly(byte[] buffer, int count)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = stream.Read(buffer, totalRead, count - totalRead);
            if (read == 0)
            {
                throw new EndOfStreamException();
            }

            totalRead += read;
        }
    }
}
