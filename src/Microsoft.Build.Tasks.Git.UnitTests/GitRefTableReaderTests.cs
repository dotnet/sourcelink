// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests;

public class GitRefTableReaderTests
{
    private static GitRefTableReader Create(params byte[] bytes)
        => new(new MemoryStream(bytes));

    private static GitRefTableReader Create(GitRefTableTestWriter writer)
    {
        writer.Position = 0;
        return new(writer.Stream);
    }

    private static byte[] GetObjectName(byte b0, ObjectNameFormat format = ObjectNameFormat.Sha1)
        => GitRefTableTestWriter.GetObjectName(b0, format);

    [Fact]
    public void ReadByte_Success()
    {
        using var reader = Create(0x12, 0x34);
        Assert.Equal((byte)0x12, reader.ReadByte());
        Assert.Equal((byte)0x34, reader.ReadByte());
    }

    [Fact]
    public void ReadByte_EndOfStream()
    {
        using var reader = Create();
        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
    }

    [Theory]
    [InlineData(new byte[] { 0x00 }, 0)]
    [InlineData(new byte[] { 0x01 }, 1)]
    [InlineData(new byte[] { 0x7F }, 127)]
    [InlineData(new byte[] { 0x80, 0x00 }, 128)]
    [InlineData(new byte[] { 0x80, 0x01 }, 129)]
    [InlineData(new byte[] { 0x80, 0x02 }, 130)]
    [InlineData(new byte[] { 0xFF, 0x7F }, 16511)]
    [InlineData(new byte[] { 0x80, 0x80, 0x00 }, 16512)]
    [InlineData(new byte[] { 0x80, 0x80, 0x01 }, 16513)]
    [InlineData(new byte[] { 0xFF, 0xFF, 0x7F }, 2113663)]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x00 }, 2113664)]
    [InlineData(new byte[] { 0xFF, 0xFF, 0xFF, 0x7F }, 270549119)]
    [InlineData(new byte[] { 0x80, 0x80, 0x80, 0x80, 0x00 }, 270549120)]
    [InlineData(new byte[] { 0x86, 0xFE, 0xFE, 0xFE, 0x7F }, int.MaxValue)]
    public void ReadVarInt_Valid(byte[] encoding, int expected)
    {
        using var reader = Create(encoding);
        Assert.Equal(expected, reader.ReadVarInt());

        var writer = new GitRefTableTestWriter();
        writer.WriteVarInt(expected);
        Assert.Equal(encoding, writer.ToArray());
    }

    [Fact]
    public void ReadVarInt_Invalid_TooManyContinuationBytes()
    {
        using var reader = Create(0x86, 0xFE, 0xFE, 0xFF, 0x7F);
        Assert.Throws<InvalidDataException>(() => reader.ReadVarInt());
    }

    [Fact]
    public void ReadUInt16BE()
    {
        using var reader = Create(0x12, 0x34);
        Assert.Equal((ushort)0x1234, reader.ReadUInt16BE());
    }

    [Fact]
    public void ReadUInt24BE()
    {
        using var reader = Create(0x01, 0x02, 0x03);
        Assert.Equal(0x10203, reader.ReadUInt24BE());
    }

    [Fact]
    public void ReadUInt32BE()
    {
        using var reader = Create(0x01, 0x02, 0x03, 0x04);
        Assert.Equal(0x01020304u, reader.ReadUInt32BE());
    }

    [Fact]
    public void ReadUInt64BE()
    {
        using var reader = Create(0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08);
        Assert.Equal(0x0102030405060708ul, reader.ReadUInt64BE());
    }

    [Fact]
    public void ReadBytes_Success()
    {
        using var reader = Create(0x10, 0x20, 0x30, 0x40);
        var data = reader.ReadBytes(4);
        Assert.Equal([0x10, 0x20, 0x30, 0x40], data);
    }

    [Fact]
    public void ReadBytes_EndOfStream()
    {
        using var reader = Create(0x10, 0x20);
        Assert.Throws<EndOfStreamException>(() => reader.ReadBytes(3));
    }

    [Fact]
    public void ReadBytes_OutOfMemory()
    {
        using var reader = Create();
        Assert.Throws<InvalidDataException>(() => reader.ReadBytes(int.MaxValue));
    }

    [Fact]
    public void ReadExactly_FillsBuffer()
    {
        using var reader = Create(0xAA, 0xBB, 0xCC);
        var buffer = new byte[3];
        reader.ReadExactly(buffer);
        Assert.Equal([0xAA, 0xBB, 0xCC], buffer);
    }

    [Fact]
    public void ReadExactly_WithCount_FillsPartialBuffer()
    {
        using var reader = Create(0xAA, 0xBB, 0xCC);
        var buffer = new byte[5];
        reader.ReadExactly(buffer, 3);
        Assert.Equal([0xAA, 0xBB, 0xCC, 0x00, 0x00], buffer);
    }

    [Fact]
    public void ReadExactly_EndOfStream()
    {
        using var reader = Create(0xAA);
        var buffer = new byte[2];
        Assert.Throws<EndOfStreamException>(() => reader.ReadExactly(buffer));
    }

    [Theory]
    [InlineData(new byte[] { 0x00 }, "00")]
    [InlineData(new byte[] { 0x01, 0x02, 0x0A }, "01020a")]
    public void ReadObjectName(byte[] bytes, string expected)
    {
        using var reader = Create(bytes);
        var actual = reader.ReadObjectName(bytes.Length);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ReadExactly_MultipleUnderlyingReads()
    {
        var data = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        using var stream = new ChunkedStream(new MemoryStream(data), maxChunk: 3);
        using var reader = new GitRefTableReader(stream);
        var buffer = new byte[data.Length];
        reader.ReadExactly(buffer);
        Assert.Equal(data, buffer);
        Assert.True(stream.ReadCallCount > 1, "Expected multiple Read calls");
    }

    private sealed class ChunkedStream(Stream inner, int maxChunk) : Stream
    {
        public int ReadCallCount { get; private set; }

        public override bool CanRead => true;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var toRead = Math.Min(maxChunk, count);
            var n = inner.Read(buffer, offset, toRead);
            if (n > 0) ReadCallCount++;
            return n;
        }
    }

    [Fact]
    public void ReadHeader_InvalidMagic()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteHeader(
            magic: 0xDEADBEEF,
            version: 1,
            blockSize: 0x100,
            minUpdate: 0,
            maxUpdate: 0);

        using var reader = Create(writer.ToArray());
        Assert.Throws<InvalidDataException>(() => reader.ReadHeader());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void ReadHeader_Version_Unsupported(byte version)
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteHeader(
            magic: 0x52454654, // 'RFTB'
            version: version,
            blockSize: 0x010203,
            minUpdate: 0,
            maxUpdate: 0);

        using var reader = Create(writer.ToArray());
        Assert.Throws<InvalidDataException>(() => reader.ReadHeader());
    }

    [Fact]
    public void ReadHeader_Version1()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteHeader(
            magic: 0x52454654, // 'RFTB'
            version: 1,
            blockSize: 0x010203,
            minUpdate: 0,
            maxUpdate: 0);

        using var reader = Create(writer.ToArray());
        var header = reader.ReadHeader();

        Assert.Equal(24, header.Size);
        Assert.Equal(0x010203, header.BlockSize);
        Assert.Equal(ObjectNameFormat.Sha1, header.ObjectNameFormat);
    }

    [Theory]
    [InlineData("sha1", ObjectNameFormat.Sha1)]
    [InlineData("s256", ObjectNameFormat.Sha256)]
    internal void ReadHeader_Version2(string hashId, ObjectNameFormat format)
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteHeader(
            magic: 0x52454654, // 'RFTB'
            version: 2,
            blockSize: 0x000102,
            minUpdate: 1,
            maxUpdate: 2,
            hashId: hashId);

        using var reader = Create(writer.ToArray());
        var header = reader.ReadHeader();
        Assert.Equal(28, header.Size);
        Assert.Equal(0x102, header.BlockSize);
        Assert.Equal(format, header.ObjectNameFormat);
    }

    [Theory]
    [InlineData("SHA1")]
    [InlineData("sha2")]
    public void ReadHeader_Version2_InvalidHashId(string hashId)
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteHeader(
            magic: 0x52454654, // 'RFTB'
            version: 2,
            blockSize: 0x000102,
            minUpdate: 1,
            maxUpdate: 2,
            hashId: hashId);

        using var reader = Create(writer.ToArray());
        Assert.Throws<InvalidDataException>(() => reader.ReadHeader());
    }

    [Fact]
    public void ReadFooter_Success()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteFooter(
            writer => writer.WriteHeader(
                magic: 0x52454654, // 'RFTB'
                version: 2,
                blockSize: 0x500,
                minUpdate: 10,
                maxUpdate: 20,
                hashId: "sha1"),
            refIndexPosition: 0,
            objPosition: 1,
            objIndexPosition: 2,
            logPosition: 3,
            logIndexPosition: 4);

        using var reader = Create(writer.ToArray());
        var footer = reader.ReadFooter();
        Assert.Equal(28, footer.Header.Size);
        Assert.Equal(0x500, footer.Header.BlockSize);
        Assert.Equal(ObjectNameFormat.Sha1, footer.Header.ObjectNameFormat);
        Assert.Equal(0, footer.RefIndexPosition);
    }

    [Fact]
    public void ReadFooter_InvalidChecksum()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteFooter(
            writer => writer.WriteHeader(
                magic: 0x52454654,
                version: 1,
                blockSize: 0x000100,
                minUpdate: 0,
                maxUpdate: 0,
                hashId: "s256"),
            refIndexPosition: 0,
            objPosition: 0,
            objIndexPosition: 0,
            logPosition: 0,
            logIndexPosition: 0);

        var footerBytes = writer.ToArray();

        // Corrupt checksum (last4 bytes)
        footerBytes[^1] ^= 0xFF;

        using var reader = Create(footerBytes);
        Assert.Throws<InvalidDataException>(() => reader.ReadFooter());
    }

    [Fact]
    public void ReadFooter_InvalidRefIndexPosition()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteFooter(
            writer => writer.WriteHeader(
                magic: 0x52454654,
                version: 2,
                blockSize: 0x000200,
                minUpdate: 0,
                maxUpdate: 0,
                hashId: "sha1"),
            refIndexPosition: 10_000_000,
            objPosition: 0,
            objIndexPosition: 0,
            logPosition: 0,
            logIndexPosition: 0);

        using var reader = Create(writer.ToArray());
        Assert.Throws<InvalidDataException>(() => reader.ReadFooter());
    }

    [Fact]
    public void ReadRefRecord_Deletion()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefRecord(
            prefixLength: 0,
            valueType: 0, // deletion
            suffix: Encoding.ASCII.GetBytes("refs/heads/main"),
            updateIndexDelta: 0);

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var record = reader.ReadRefRecord(header, priorName: "");
        Assert.Equal("refs/heads/main", record.RefName);
        Assert.Null(record.ObjectName);
        Assert.Null(record.SymbolicRef);
    }

    [Fact]
    public void ReadRefRecord_ObjectName()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefRecord(
            prefixLength: 0,
            valueType: 1, // object name
            suffix: Encoding.ASCII.GetBytes("refs/heads/main"),
            updateIndexDelta: 0,
            objectName: GetObjectName(0x01));

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var record = reader.ReadRefRecord(header, priorName: "");
        Assert.Equal("refs/heads/main", record.RefName);
        Assert.Equal("0100000000000000000000000000000000000000", record.ObjectName);
        Assert.Null(record.SymbolicRef);
    }

    [Fact]
    public void ReadRefRecord_ObjectNamePeeled()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefRecord(
            prefixLength: 0,
            valueType: 2, // object name and peeled target
            suffix: Encoding.ASCII.GetBytes("refs/heads/main"),
            updateIndexDelta: 0,
            objectName: GetObjectName(0x12),
            peeledObjectName: GetObjectName(0x34));

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var record = reader.ReadRefRecord(header, priorName: "");
        Assert.Equal("refs/heads/main", record.RefName);
        Assert.Equal("1200000000000000000000000000000000000000", record.ObjectName);
        Assert.Null(record.SymbolicRef);
    }

    [Fact]
    public void ReadRefRecord_SymbolicRef()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefRecord(
            prefixLength: 0,
            valueType: 3, // symbolic ref
            suffix: Encoding.ASCII.GetBytes("refs/heads/main"),
            updateIndexDelta: 0,
            symbolicRef: Encoding.ASCII.GetBytes("refs/heads/foo"));

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var record = reader.ReadRefRecord(header, priorName: "");
        Assert.Equal("refs/heads/main", record.RefName);
        Assert.Null(record.ObjectName);
        Assert.Equal("refs/heads/foo", record.SymbolicRef);
    }

    [Fact]
    public void ReadRefRecord_SymbolicRef_InvalidEncoding()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefRecord(
            prefixLength: 0,
            valueType: 3, // symbolic ref
            suffix: Encoding.ASCII.GetBytes("refs/heads/main"),
            updateIndexDelta: 0,
            symbolicRef: [0x00, 0xD8]); // U+D800: unpaired surrogate

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        Assert.Throws<InvalidDataException>(() => reader.ReadRefRecord(header, priorName: ""));
    }

    [Fact]
    public void ReadRefRecord_Prefix()
    {
        var writer = new GitRefTableTestWriter();

        var prefix = "refs/heads/";

        writer.WriteRefRecord(
            prefixLength: prefix.Length,
            valueType: 0, // deletion
            suffix: Encoding.ASCII.GetBytes("main"),
            updateIndexDelta: 0);

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var record = reader.ReadRefRecord(header, priorName: prefix + "foo");
        Assert.Equal("refs/heads/main", record.RefName);
    }

    [Fact]
    public void ReadRefRecord_InvalidPrefixLength()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefRecord(
            prefixLength: 100,
            valueType: 0, // deletion
            suffix: Encoding.ASCII.GetBytes("main"),
            updateIndexDelta: 0);

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        Assert.Throws<InvalidDataException>(() => reader.ReadRefRecord(header, priorName: "foo"));
    }

    [Fact]
    public void ReadRefRecord_InvalidEncoding()
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefRecord(
            prefixLength: 0,
            valueType: 0, // deletion
            suffix: [0x00, 0xD8], // U+D800: unpaired surrogate
            updateIndexDelta: 0);

        using var reader = Create(writer);

        var header = new GitRefTableReader.Header()
        {
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        Assert.Throws<InvalidDataException>(() => reader.ReadRefRecord(header, priorName: ""));
    }

    [Fact]
    public void ReadRefIndexRecord_Prefix()
    {
        var writer = new GitRefTableTestWriter();

        var prefix = "refs/heads/";

        writer.WriteRefIndexRecord(
            prefixLength: prefix.Length,
            valueType: 0, // deletion
            suffix: Encoding.ASCII.GetBytes("main"),
            blockPosition: 12345);

        using var reader = Create(writer);

        var record = reader.ReadRefIndexRecord(priorName: prefix + "foo");
        Assert.Equal("refs/heads/main", record.LastRefName);
        Assert.Equal(12345, record.BlockPosition);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ReadRefIndexRecord_InvalidType(byte type)
    {
        var writer = new GitRefTableTestWriter();

        writer.WriteRefIndexRecord(
            prefixLength: 0,
            valueType: type,
            suffix: Encoding.ASCII.GetBytes("main"),
            blockPosition: 1234);

        using var reader = Create(writer);
        Assert.Throws<InvalidDataException>(() => reader.ReadRefIndexRecord(priorName: ""));
    }

    [Theory]
    [CombinatorialData]
    public void ReadRestartOffsets(bool isFirst)
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 10,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var offset1 = 0;
        var offset2 = 0;

        writer.WriteBlock(isFirst ? header : null, 'r', (writer, blockStart) =>
        {
            // ref_record+
            offset1 = (int)(writer.Position - blockStart);

            writer.WriteRefRecord(
                prefixLength: 0,
                valueType: 1, // object name
                suffix: Encoding.ASCII.GetBytes("refs/heads/main"),
                updateIndexDelta: 0,
                objectName: GetObjectName(0x01));

            writer.WriteRefRecord(
                prefixLength: "refs/heads/".Length,
                valueType: 1, // object name
                suffix: Encoding.ASCII.GetBytes("foo"),
                updateIndexDelta: 0,
                objectName: GetObjectName(0x02));

            writer.WriteRefRecord(
                prefixLength: "refs/heads/".Length,
                valueType: 1, // object name
                suffix: Encoding.ASCII.GetBytes("bar"),
                updateIndexDelta: 0,
                objectName: GetObjectName(0x03));

            offset2 = (int)writer.Position;

            writer.WriteRefRecord(
                prefixLength: 0,
                valueType: 1, // object name
                suffix: Encoding.ASCII.GetBytes("baz"),
                updateIndexDelta: 0,
                objectName: GetObjectName(0x04));

            // uint24(restart_offset)+
            writer.WriteRestartOffsets([offset1, offset2]);
        });

        using var reader = Create(writer);

        if (isFirst)
        {
            _ = reader.ReadHeader();
        }

        Assert.Equal((byte)'r', reader.ReadByte());

        var offsets = reader.ReadRestartOffsets(header, blockLengthLimited: false, out var blockStart, out var blockEnd);

        Assert.Equal([offset1, offset2, (byte)(blockEnd - sizeof(ushort) - 2 * 3)], offsets);
        Assert.Equal(0, blockStart);
        Assert.Equal(writer.Stream.Length, blockEnd);
    }

    [Fact]
    public void ReadRestartOffsets_InvalidBlockLength_LargerThanHeaderBlockSize()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 0x100,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        writer.WriteBlock(null, 'r', (writer, _) =>
        {
            // dummy content:
            writer.Stream.WriteByte(0);
        });

        using var reader = Create(writer);

        Assert.Equal((byte)'r', reader.ReadByte());

        Assert.Throws<InvalidDataException>(() => reader.ReadRestartOffsets(header, blockLengthLimited: true, out _, out _));
    }

    [Fact]
    public void ReadRestartOffsets_InvalidBlockLength_BlockSizeTooSmall()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 1,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        writer.WriteBlock(null, 'r', (writer, _) =>
        {
            // dummy content:
            writer.Stream.WriteByte(0);
        });

        using var reader = Create(writer);

        Assert.Equal((byte)'r', reader.ReadByte());

        Assert.Throws<InvalidDataException>(() => reader.ReadRestartOffsets(header, blockLengthLimited: false, out _, out _));
    }

    public enum OffsetError
    {
        Empty,
        Unordered,
        OutOfBlock,
    }

    [Theory]
    [CombinatorialData]
    public void ReadRestartOffsets_Unordered(OffsetError error)
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 10,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var offset1 = 0;
        var offset2 = 0;

        writer.WriteBlock(header: null, 'r', (writer, blockStart) =>
        {
            // ref_record+
            offset1 = (int)(writer.Position - blockStart);

            writer.WriteRefRecord(
                prefixLength: 0,
                valueType: 1, // object name
                suffix: Encoding.ASCII.GetBytes("refs/heads/main1"),
                updateIndexDelta: 0,
                objectName: GetObjectName(0x01));

            writer.WriteRefRecord(
                prefixLength: "refs/heads/".Length,
                valueType: 1, // object name
                suffix: Encoding.ASCII.GetBytes("foo"),
                updateIndexDelta: 0,
                objectName: GetObjectName(0x02));

            offset2 = (int)(writer.Position - blockStart);

            writer.WriteRefRecord(
                prefixLength: 0,
                valueType: 1, // object name
                suffix: Encoding.ASCII.GetBytes("refs/heads/main2"),
                updateIndexDelta: 0,
                objectName: GetObjectName(0x03));

            // uint24(restart_offset)+
            writer.WriteRestartOffsets(
                error switch
                {
                    OffsetError.Empty => [],
                    OffsetError.Unordered => [offset2, offset1],
                    OffsetError.OutOfBlock => [offset1, 10000],
                    _ => throw new InvalidOperationException()
                });
        });

        using var reader = Create(writer);

        Assert.Equal((byte)'r', reader.ReadByte());

        Assert.Throws<InvalidDataException>(() => reader.ReadRestartOffsets(header, blockLengthLimited: false, out _, out _));
    }

    [Fact]
    public void SearchBlock_RefRecord()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 1000,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var refBlock1 = writer.WriteRefBlock(header: null,
        [
            ("", "refs/heads/a", 0x01),
            ("refs/heads/", "b", 0x02),
            ("refs/heads/", "c", 0x03),
            ("", "refs/heads/d", 0x04),
        ]);

        using var reader = Create(writer);

        writer.Position = refBlock1;
        var record = reader.SearchBlock(header, "refs/heads/c");
        Assert.NotNull(record);
        Assert.Equal("refs/heads/c", record.Value.RefName);
        Assert.Equal("0300000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads/a");
        Assert.NotNull(record);
        Assert.Equal("refs/heads/a", record.Value.RefName);
        Assert.Equal("0100000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads/b");
        Assert.NotNull(record);
        Assert.Equal("refs/heads/b", record.Value.RefName);
        Assert.Equal("0200000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads/d");
        Assert.NotNull(record);
        Assert.Equal("refs/heads/d", record.Value.RefName);
        Assert.Equal("0400000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads");
        Assert.Null(record);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads/aa");
        Assert.Null(record);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads/bb");
        Assert.Null(record);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads/cc");
        Assert.Null(record);

        writer.Position = 0;
        record = reader.SearchBlock(header, "refs/heads/dd");
        Assert.Null(record);
    }

    [Fact]
    public void SearchBlock_RefIndexRecord()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 1000,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        // I3 -> I1 -> R1
        //             R2
        //       I2 -> R3
        //             R4
        //       R5

        var refBlock1 = writer.WriteRefBlock(header,
        [
            ("", "refs/heads/a", 0x01),
            ("refs/heads/", "b", 0x02),
            ("refs/heads/", "c", 0x03),
        ]);

        var refBlock2 = writer.WriteRefBlock(header: null,
        [
            ("", "refs/heads/d", 0x04),
            ("refs/heads/", "e", 0x05),
        ]);

        var refBlock3 = writer.WriteRefBlock(header: null,
        [
            ("", "refs/heads/f", 0x06),
        ]);

        var refBlock4 = writer.WriteRefBlock(header: null,
        [
            ("", "refs/heads/g", 0x07),
        ]);

        var refBlock5 = writer.WriteRefBlock(header: null,
        [
            ("", "refs/heads/h", 0x08),
        ]);

        var refIndexBlock1 = writer.WriteRefIndexBlock(
        [
            ("", "refs/heads/c", refBlock1),
            ("refs/heads/", "e", refBlock2),
        ]);

        var refIndexBlock2 = writer.WriteRefIndexBlock(
        [
            ("", "refs/heads/f", refBlock3),
            ("refs/heads/", "g", refBlock4),
        ]);

        var refIndexBlock3 = writer.WriteRefIndexBlock(
        [
            ("", "refs/heads/e", refIndexBlock1),
            ("refs/heads/", "g", refIndexBlock2),
            ("", "refs/heads/h", refBlock5),
        ]);

        using var reader = Create(writer);

        writer.Position = refIndexBlock3;
        var record = reader.SearchBlock(header, "refs/heads/a");
        Assert.NotNull(record);
        Assert.Equal("0100000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/b");
        Assert.NotNull(record);
        Assert.Equal("0200000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/c");
        Assert.NotNull(record);
        Assert.Equal("0300000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/d");
        Assert.NotNull(record);
        Assert.Equal("0400000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/e");
        Assert.NotNull(record);
        Assert.Equal("0500000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/f");
        Assert.NotNull(record);
        Assert.Equal("0600000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/g");
        Assert.NotNull(record);
        Assert.Equal("0700000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/h");
        Assert.NotNull(record);
        Assert.Equal("0800000000000000000000000000000000000000", record.Value.ObjectName);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/aa");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/bb");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/cc");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/dd");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/ee");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/ff");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/gg");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/hh");
        Assert.Null(record);

        writer.Position = refIndexBlock3;
        record = reader.SearchBlock(header, "refs/heads/z");
        Assert.Null(record);
    }

    [Fact]
    public void TryFindReference_RefIndex()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 1000,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var refBlock1 = writer.WriteRefBlock(header,
        [
            ("", "refs/heads/a", 0x01),
            ("refs/heads/", "b", "sym-ref"),
        ]);

        var refIndexBlock1 = writer.WriteRefIndexBlock(
        [
            ("", "refs/heads/b", refBlock1),
        ]);

        writer.WriteFooter(header, refIndexBlock1);

        using var reader = Create(writer);

        Assert.True(reader.TryFindReference("refs/heads/a", out var objectName, out var symRef));
        Assert.Equal("0100000000000000000000000000000000000000", objectName);
        Assert.Null(symRef);

        Assert.True(reader.TryFindReference("refs/heads/b", out objectName, out symRef));
        Assert.Equal("sym-ref", symRef);
        Assert.Null(objectName);

        Assert.False(reader.TryFindReference("refs/heads/c", out objectName, out symRef));
        Assert.Null(symRef);
        Assert.Null(objectName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void TryFindReference_NoRefIndex(int blockSize)
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = blockSize,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        writer.WriteRefBlock(header,
        [
            ("", "refs/heads/a", 0x01),
            ("refs/heads/", "b", "sym-ref"),
        ]);

        writer.WritePadding(blockSize);

        writer.WriteRefBlock(header: null,
        [
            ("", "refs/heads/c", 0x02),
        ]);

        writer.WritePadding(blockSize);

        writer.WriteFooter(header, refIndexPosition: 0);

        using var reader = Create(writer);

        Assert.True(reader.TryFindReference("refs/heads/a", out var objectName, out var symRef));
        Assert.Equal("0100000000000000000000000000000000000000", objectName);
        Assert.Null(symRef);

        Assert.True(reader.TryFindReference("refs/heads/b", out objectName, out symRef));
        Assert.Equal("sym-ref", symRef);
        Assert.Null(objectName);

        Assert.True(reader.TryFindReference("refs/heads/c", out objectName, out symRef));
        Assert.Equal("0200000000000000000000000000000000000000", objectName);
        Assert.Null(symRef);

        Assert.False(reader.TryFindReference("refs/heads/d", out objectName, out symRef));
        Assert.Null(symRef);
        Assert.Null(objectName);
    }

    [Fact]
    public void TryFindReference_RefBlockFollowedByObjBlock()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 0,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        writer.WriteRefBlock(header,
        [
            ("", "refs/heads/a", 0x01)
        ]);

        writer.WriteBlock(header, 'o', (writer, _) =>
        {
            writer.Stream.WriteByte(0);
        });

        writer.WriteFooter(header, refIndexPosition: 0);

        using var reader = Create(writer);

        Assert.True(reader.TryFindReference("refs/heads/a", out var objectName, out var symRef));
        Assert.Equal("0100000000000000000000000000000000000000", objectName);
        Assert.Null(symRef);

        Assert.False(reader.TryFindReference("refs/heads/b", out objectName, out symRef));
        Assert.Null(objectName);
        Assert.Null(symRef);
    }

    [Fact]
    public void TryFindReference_InvalidBlockType()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 0,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        var refBlock1 = writer.WriteRefBlock(header,
        [
            ("", "refs/heads/a", 0x01)
        ]);

        writer.WriteRefIndexBlock(
        [
            ("", "refs/heads/b", refBlock1),
        ]);

        writer.WriteFooter(header, refIndexPosition: 0);

        using var reader = Create(writer);

        Assert.True(reader.TryFindReference("refs/heads/a", out var objectName, out var symRef));
        Assert.Equal("0100000000000000000000000000000000000000", objectName);
        Assert.Null(symRef);

        Assert.Throws<InvalidDataException>(() => reader.TryFindReference("refs/heads/b", out _, out _));
    }

    [Fact]
    public void TryFindReference_NoRefBlock()
    {
        var writer = new GitRefTableTestWriter();

        var header = new GitRefTableReader.Header()
        {
            Size = 24,
            BlockSize = 0,
            ObjectNameFormat = ObjectNameFormat.Sha1
        };

        writer.WriteBlock(header, 'o', (writer, _) =>
        {
            writer.Stream.WriteByte(0);
        });

        writer.WriteFooter(header, refIndexPosition: 0);

        using var reader = Create(writer);
        Assert.Throws<InvalidDataException>(() => reader.TryFindReference("refs/heads/a", out _, out _));
    }
}
