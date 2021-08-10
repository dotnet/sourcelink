// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;

namespace Microsoft.Build.Tasks.Git
{
    internal readonly struct GitSubmodule
    {
        public string Name { get; }

        /// <summary>
        /// Working directory path as specified in .gitmodules file.
        /// Expected to be relative to the working directory of the containing repository and have Posix directory separators (not normalized).
        /// </summary>
        public string WorkingDirectoryRelativePath { get; }

        /// <summary>
        /// Normalized full path.
        /// </summary>
        public string WorkingDirectoryFullPath { get; }

        /// <summary>
        /// An absolute URL or a relative path (if it starts with `./` or `../`) to the origin remote of the containing repository.
        /// </summary>
        public string? Url { get; }

        /// <summary>
        /// Head tip commit SHA. Null, if there is no commit.
        /// </summary>
        public string? HeadCommitSha { get; }

        internal GitSubmodule(string name, string workingDirectoryRelativePath, string workingDirectoryFullPath, string? url, string? headCommitSha)
        {
            NullableDebug.Assert(name != null);
            NullableDebug.Assert(workingDirectoryRelativePath != null);
            NullableDebug.Assert(workingDirectoryFullPath != null);

            Name = name;
            WorkingDirectoryRelativePath = workingDirectoryRelativePath;
            WorkingDirectoryFullPath = workingDirectoryFullPath;
            Url = url;
            HeadCommitSha = headCommitSha;
        }
    }
}
