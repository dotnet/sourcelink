// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;

namespace Microsoft.Build.Tasks.Git
{
    internal readonly struct GitRepositoryLocation
    {
        /// <summary>
        /// Normalized full path. OS specific directory separators.
        /// </summary>
        public readonly string GitDirectory { get; }

        /// <summary>
        /// Normalized full path. OS specific directory separators.
        /// </summary>
        public readonly string CommonDirectory { get; }

        /// <summary>
        /// Normalized full path. OS specific directory separators. Optional.
        /// </summary>
        public readonly string? WorkingDirectory { get; }

        internal GitRepositoryLocation(string gitDirectory, string commonDirectory, string? workingDirectory)
        {
            NullableDebug.Assert(PathUtils.IsNormalized(gitDirectory));
            NullableDebug.Assert(PathUtils.IsNormalized(commonDirectory));
            NullableDebug.Assert(workingDirectory == null || PathUtils.IsNormalized(workingDirectory));

            GitDirectory = gitDirectory;
            CommonDirectory = commonDirectory;
            WorkingDirectory = workingDirectory;
        }
    }
}
