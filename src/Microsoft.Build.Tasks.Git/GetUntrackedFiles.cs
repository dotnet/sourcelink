// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    /// <summary>
    /// Selects files that are under the repository root but ignored.
    /// </summary>
    public sealed class GetUntrackedFiles : RepositoryTask
    {
        public string? RepositoryId { get; set; }

        [Required, NotNull]
        public ITaskItem[]? Files { get; set; }

        [Required, NotNull]
        public string? ProjectDirectory { get; set; }

        [Output]
        public ITaskItem[]? UntrackedFiles { get; private set; }

        protected override string? GetRepositoryId() => RepositoryId;
        protected override string GetInitialPath() => ProjectDirectory;

        private protected override void Execute(GitRepository repository)
        {
            UntrackedFiles = GitOperations.GetUntrackedFiles(repository, Files, ProjectDirectory);
        }
    }
}
