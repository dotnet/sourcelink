// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;

namespace Microsoft.Build.Tasks.Git
{
    internal static class GitReftableReader
    {
        // See https://git-scm.com/docs/reftable
        private const uint ReftableMagic = 0x52454654; // 'REFT'
        private const byte BlockTypeRef = 0x72; // 'r'
        private const byte BlockTypeObj = 0x6f; // 'o'
        private const byte BlockTypeLog = 0x67; // 'g'
        private const byte BlockTypeIndex = 0x69; // 'i'

        private const int HeaderSize = 24;
        private const int FooterSize = 68;

        public static ImmutableDictionary<string, string> ReadReftableReferences(string reftableDirectory)
        {
            // https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-reftable
            
            if (!Directory.Exists(reftableDirectory))
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            var builder = ImmutableDictionary.CreateBuilder<string, string>();

            try
            {
                // Read all .ref files in the reftable directory
                var refFiles = Directory.GetFiles(reftableDirectory, "*.ref");
                
                // Sort to ensure we process them in order (newer files override older ones)
                Array.Sort(refFiles, StringComparer.Ordinal);

                foreach (var refFile in refFiles)
                {
                    try
                    {
                        ReadReftableFile(refFile, builder);
                    }
                    catch
                    {
                        // If we can't read a file, skip it and try others
                        continue;
                    }
                }
            }
            catch
            {
                // If we can't read the reftable directory, return empty
                return ImmutableDictionary<string, string>.Empty;
            }

            return builder.ToImmutable();
        }

        private static void ReadReftableFile(string path, ImmutableDictionary<string, string>.Builder builder)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);

            // Read and validate header
            var header = ReadHeader(reader);
            if (header == null)
            {
                return;
            }

            // Seek to the first block (after header)
            stream.Seek(HeaderSize, SeekOrigin.Begin);

            // Read blocks until we reach the footer
            long footerStart = stream.Length - FooterSize;
            while (stream.Position < footerStart)
            {
                long blockStart = stream.Position;
                
                // Read block header
                if (stream.Length - stream.Position < 4)
                {
                    break;
                }

                byte[] blockHeader = reader.ReadBytes(4);
                if (blockHeader.Length < 4)
                {
                    break;
                }

                byte blockType = blockHeader[0];
                
                // Block size is stored in bytes 1-3 (24-bit big-endian)
                int blockSize = (blockHeader[1] << 16) | (blockHeader[2] << 8) | blockHeader[3];

                if (blockSize == 0 || blockSize > stream.Length - blockStart)
                {
                    break;
                }

                // Only process ref blocks
                if (blockType == BlockTypeRef)
                {
                    ReadRefBlock(reader, blockStart, blockSize, builder);
                }

                // Move to next block
                stream.Seek(blockStart + blockSize, SeekOrigin.Begin);
            }
        }

        private static ReftableHeader? ReadHeader(BinaryReader reader)
        {
            try
            {
                uint magic = ReadUInt32BE(reader);
                if (magic != ReftableMagic)
                {
                    return null;
                }

                uint version = ReadUInt32BE(reader);
                if (version != 1)
                {
                    return null; // Only version 1 is supported
                }

                // Read the rest of the header
                ulong minUpdateIndex = ReadUInt64BE(reader);
                ulong maxUpdateIndex = ReadUInt64BE(reader);

                return new ReftableHeader
                {
                    Version = version,
                    MinUpdateIndex = minUpdateIndex,
                    MaxUpdateIndex = maxUpdateIndex
                };
            }
            catch
            {
                return null;
            }
        }

        private static void ReadRefBlock(BinaryReader reader, long blockStart, int blockSize, ImmutableDictionary<string, string>.Builder builder)
        {
            try
            {
                long blockEnd = blockStart + blockSize;
                
                // Skip the 4-byte block header we already read
                long dataStart = blockStart + 4;
                reader.BaseStream.Seek(dataStart, SeekOrigin.Begin);

                // Reset last ref name for this block
                _lastRefName = "";

                // Read restart points count (last 2 bytes before block end, excluding padding)
                // For simplicity, we'll do a sequential scan instead of using restart points
                
                while (reader.BaseStream.Position < blockEnd - 2)
                {
                    var (refName, objectId) = ReadRefRecord(reader);
                    
                    if (refName == null || objectId == null)
                    {
                        break;
                    }

                    // Store the reference (later entries override earlier ones)
                    builder[refName] = objectId;
                }
            }
            catch
            {
                // Skip this block if we can't read it
            }
        }

        private static string _lastRefName = "";

        private static (string? RefName, string? ObjectId) ReadRefRecord(BinaryReader reader)
        {
            try
            {
                // Read varint for prefix length
                int prefixLength = ReadVarint(reader);
                if (prefixLength < 0)
                {
                    return (null, null);
                }

                // Read varint for suffix length  
                int suffixLength = ReadVarint(reader);
                if (suffixLength < 0)
                {
                    return (null, null);
                }

                // Read suffix
                byte[] suffixBytes = reader.ReadBytes(suffixLength);
                if (suffixBytes.Length != suffixLength)
                {
                    return (null, null);
                }

                string suffix = Encoding.UTF8.GetString(suffixBytes);
                
                // Build full reference name using prefix compression
                string refName;
                if (prefixLength == 0)
                {
                    refName = suffix;
                }
                else if (prefixLength <= _lastRefName.Length)
                {
                    refName = _lastRefName.Substring(0, prefixLength) + suffix;
                }
                else
                {
                    // Invalid prefix length
                    return (null, null);
                }

                _lastRefName = refName;

                // Read value type
                byte valueType = reader.ReadByte();

                string? objectId = null;

                // Value type bits:
                // 0x1 = has value1 (object ID)
                // 0x2 = has value2 (peeled)
                // 0x4 = has symref
                if ((valueType & 0x1) != 0)
                {
                    // Read object ID (20 bytes for SHA-1)
                    byte[] oid = reader.ReadBytes(20);
                    if (oid.Length == 20)
                    {
                        objectId = BitConverter.ToString(oid).Replace("-", "").ToLowerInvariant();
                    }
                }

                if ((valueType & 0x2) != 0)
                {
                    // Skip peeled object ID
                    reader.ReadBytes(20);
                }

                if ((valueType & 0x4) != 0)
                {
                    // Skip symref (varint length + string)
                    int symrefLength = ReadVarint(reader);
                    if (symrefLength > 0)
                    {
                        reader.ReadBytes(symrefLength);
                    }
                }

                return (refName, objectId);
            }
            catch
            {
                return (null, null);
            }
        }

        private static int ReadVarint(BinaryReader reader)
        {
            try
            {
                int result = 0;
                int shift = 0;

                while (true)
                {
                    byte b = reader.ReadByte();
                    result |= (b & 0x7F) << shift;
                    
                    if ((b & 0x80) == 0)
                    {
                        break;
                    }
                    
                    shift += 7;
                    
                    if (shift > 28)
                    {
                        return -1; // Overflow
                    }
                }

                return result;
            }
            catch
            {
                return -1;
            }
        }

        private static uint ReadUInt32BE(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(4);
            if (bytes.Length != 4)
            {
                throw new EndOfStreamException();
            }
            
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static ulong ReadUInt64BE(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(8);
            if (bytes.Length != 8)
            {
                throw new EndOfStreamException();
            }
            
            return ((ulong)bytes[0] << 56) | ((ulong)bytes[1] << 48) | ((ulong)bytes[2] << 40) | ((ulong)bytes[3] << 32) |
                   ((ulong)bytes[4] << 24) | ((ulong)bytes[5] << 16) | ((ulong)bytes[6] << 8) | bytes[7];
        }

        private class ReftableHeader
        {
            public uint Version { get; set; }
            public ulong MinUpdateIndex { get; set; }
            public ulong MaxUpdateIndex { get; set; }
        }
    }
}
