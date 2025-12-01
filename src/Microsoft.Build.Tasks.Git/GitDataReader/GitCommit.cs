// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed partial class GitCommit
    {
        public string Hash { get; }
        public DateTimeOffset? CommitTimestamp { get; }

        private GitCommit(string hash, DateTimeOffset? commitTimestamp)
        {
            Hash = hash;
            CommitTimestamp = commitTimestamp;
        }

        internal static GitCommit FromHash(string GitDirectory, string hash)
        {
#if NET6_0_OR_GREATER
            var directoryPath = hash[..2];
            var filePath = hash[2..];
            var commitObjectPath = Path.Combine(GitDirectory, "objects", directoryPath, filePath);
            if (File.Exists(commitObjectPath))
            {
                using var fileStream = File.Open(commitObjectPath, FileMode.Open, FileAccess.Read);
                using var zStream = new ZLibStream(fileStream, CompressionMode.Decompress);
                using var memoryStream = new MemoryStream();
                zStream.CopyTo(memoryStream);
                var contents = Encoding.UTF8.GetString(memoryStream.ToArray());
                var expectedMagic = "commit ";
                if (!contents.StartsWith(expectedMagic, StringComparison.Ordinal))
                {
                    // TODO: Console.Write("Commit does not begin with magic string");
                    return new GitCommit(hash, null);
                }

                int nullIndex = contents.IndexOf('\0', StringComparison.Ordinal);
                if (nullIndex < 0)
                {
                    // TODO: Console.Write("Cound not find null");
                    return new GitCommit(hash, null);
                }

                contents = contents[(nullIndex+1)..];

                var lines = contents.Split('\n');
                foreach (var line in lines)
                {
                    if (line.StartsWith("committer "))
                    {
                        var parts = line.Split(' ');
                        var timestamp = parts[^2];
                        var timezone = parts[^1];
                        Console.WriteLine($"Found commit date: {timestamp} {timezone}");

                        return new GitCommit(hash, ParseGitTimestamp(timestamp, timezone));
                    }
                    else
                    {
                        continue;
                    }
                }
                // TODO: error: could not parse commit file
                return new GitCommit(hash, null);
            }
            else
            {
                // TODO: Console.WriteLine("Could not find loose object with hash. Switching to searching in packed objects");
                return new GitCommit(hash, null);
            }
#else
            return new GitCommit(hash, null);
#endif
        }

        private static DateTimeOffset? ParseGitTimestamp(string timestamp, string timezone)
        {
            var timestampLong = long.Parse(timestamp, CultureInfo.InvariantCulture);
            var date = DateTimeOffset.FromUnixTimeSeconds(timestampLong);

            var hours = int.Parse(timezone[1..3], CultureInfo.InvariantCulture);
            var minutes = int.Parse(timezone[3..5], CultureInfo.InvariantCulture);
            var offset = TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes);
            if (timezone[0] == '-')
            {
                offset = offset.Negate();
            }

            return new DateTimeOffset(date.DateTime, offset);
        }
    }
}
