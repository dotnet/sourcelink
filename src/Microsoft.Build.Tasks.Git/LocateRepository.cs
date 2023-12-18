// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    public sealed class LocateRepository : RepositoryTask
    {
        public string? RemoteName { get; set; }

        [Required, NotNull]
        public string? Path { get; set; }

        [Output]
        public string? RepositoryId { get; private set; }

        [Output]
        public string? WorkingDirectory { get; private set; }

        [Output]
        public string? Url { get; private set; }

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
        public ITaskItem[]? Roots { get; private set; }

        /// <summary>
        /// Head tip commit SHA.
        /// </summary>
        [Output]
        public string? RevisionId { get; private set; }

        protected override string? GetRepositoryId() => null;
        protected override string GetInitialPath() => Path!;

        private protected override void Execute(GitRepository repository)
        {
            NullableDebug.Assert(repository.WorkingDirectory != null);

            RepositoryId = repository.GitDirectory;
            WorkingDirectory = repository.WorkingDirectory;
            Url = GitOperations.GetRepositoryUrl(repository, RemoteName, warnOnMissingOrUnsupportedRemote: !NoWarnOnMissingInfo, Log.LogWarning);
            Roots = GitOperations.GetSourceRoots(repository, RemoteName, warnOnMissingCommitOrUnsupportedUri: !NoWarnOnMissingInfo, Log.LogWarning);
            RevisionId = repository.GetHeadCommitSha();
        }
    }
}
