// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

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
