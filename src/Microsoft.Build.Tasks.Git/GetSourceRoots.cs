﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Git;
using System.IO;

namespace SourceControlBuildTasks
{
    public sealed class GetSourceRoots : RepositoryTask
    {
        /// <summary>
        /// Returns items describing repository source roots:
        /// 
        /// Metadata
        ///   Identity: Normalized path. Ends with a directory separator.
        ///   SourceControl: "Git"
        ///   RepositoryUrl: URL of the repository.
        ///   RevisionId: Revision (commit SHA).
        ///   ContainingRoot: Identity of the containing source root.
        ///   NestedRoot: For a submodule root, a path of the submodule root relative to the repository root. Ends with a slash.
        /// </summary>
        [Output]
        public ITaskItem[] Roots { get; private set; }

        protected override void Execute(Repository repo)
        {
            Roots = repo.GetSourceRoots(Log.LogWarning, File.Exists);
        }
    }
}
