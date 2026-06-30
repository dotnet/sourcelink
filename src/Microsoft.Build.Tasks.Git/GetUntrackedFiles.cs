// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    /// <summary>
    /// Selects files that are under the repository root but ignored.
    /// </summary>
    [MSBuildMultiThreadableTask]
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
            // Absolutize ProjectDirectory against the task's project directory (MT-safe) rather than the
            // process CWD before it is used as the base for resolving the (relative) file ItemSpecs inside
            // GitOperations.GetUntrackedFiles. The absolutized path is only used internally for the ignore
            // check; the returned [Output] items keep their original ItemSpecs (Sin 1).
            AbsolutePath absProjectDir = TaskEnvironment.GetAbsolutePath(ProjectDirectory);
            UntrackedFiles = GitOperations.GetUntrackedFiles(repository, Files, absProjectDir);
        }
    }
}
