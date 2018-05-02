﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.Git;

namespace SourceControlBuildTasks
{
    /// <summary>
    /// Selects files that are under the repository root but ignored.
    /// </summary>
    public sealed class GetUntrackedFiles : RepositoryTask
    {
        [Required]
        public ITaskItem[] Files { get; set; }

        [Required]
        public string ProjectDirectory { get; set; }

        [Output]
        public ITaskItem[] UntrackedFiles { get; set; }

        protected override void Execute(Repository repo)
        {
            UntrackedFiles = repo.GetUntrackedFiles(Files, ProjectDirectory, dir => new Repository(dir));
        }
    }
}
