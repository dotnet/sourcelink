// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.Build.Tasks.Git;

internal sealed partial class GitRefTableReader
{
    internal readonly struct Header
    {
        /// <summary>
        /// Size of the header (differs between versions).
        /// </summary>
        public int Size { get; init; }

        /// <summary>
        /// Block size or 0 if blocks are unaligned.
        /// </summary>
        public int BlockSize { get; init; }

        public ObjectNameFormat ObjectNameFormat { get; init; }
    }

    internal readonly struct Footer
    {
        public const int SizeExcludingHeader = 44;

        public Header Header { get; init; }
        public long RefIndexPosition { get; init; }
    }

    internal readonly struct RefRecord
    {
        public string RefName { get; init; }
        public string? ObjectName { get; init; }
        public string? SymbolicRef { get; init; }
    }

    internal readonly struct RefIndexRecord
    {
        /// <summary>
        /// Last reference in the target block.
        /// </summary>
        public string LastRefName { get; init; }

        /// <summary>
        /// Position of leaf RefBlock or next level RefIndexBlock from the start of the file.
        /// </summary>
        public long BlockPosition { get; init; }
    }

}
