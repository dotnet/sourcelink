// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    /// <summary>
    /// Selects files that are under the repository root but ignored.
    /// </summary>
    public sealed class GetUntrackedFiles : RepositoryTask
    {
        [Required]
        public ITaskItem[] Files { get; set; }

        [Output]
        public ITaskItem[] UntrackedFiles { get; set; }

        protected override bool Execute(Repository repo)
        {
            UntrackedFiles = repo.GetUntrackedFiles(Files);
            return true;
        }
    }
}
