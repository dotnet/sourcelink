// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.Build.Tasks.Git
{
    internal readonly struct GitSubmodule
    {
        public string Name { get; }

        /// <summary>
        /// Working directory path as specified in .gitmodules file.
        /// Expected to be relative to the working directory of the containing repository and have Posix directory separators (not normalized).
        /// </summary>
        public string WorkingDirectoryPath { get; }

        /// <summary>
        /// An absolute URL or a relative path (if it starts with `./` or `../`) to the default remote of the containing repository.
        /// </summary>
        public string Url { get; }

        public GitSubmodule(string name, string workingDirectoryPath, string url)
        {
            Name = name;
            WorkingDirectoryPath = workingDirectoryPath;
            Url = url;
        }
    }
}
