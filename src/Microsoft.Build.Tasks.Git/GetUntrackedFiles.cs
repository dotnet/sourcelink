// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;

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
            // ProjectDirectory is the base for resolving the (relative) file ItemSpecs below, so it must be
            // fully qualified: a relative base would be resolved against the shared process current working
            // directory, which is unsafe under the multithreaded task model. Reaching this method with a
            // non-fully-qualified ProjectDirectory is only possible on the cached-repository path (RepositoryId
            // set), where the base RepositoryTask serves the repository from the cache without validating the
            // initial path. ProjectDirectory is expected to be absolute ($(MSBuildProjectDirectory) in the
            // shipped targets), so a non-absolute value is an error rather than a "missing repository" condition.
            if (!PathUtilities.IsPathFullyQualified(ProjectDirectory))
            {
                Log.LogError(Resources.PathMustBeAbsolute);
                return;
            }

            UntrackedFiles = GitOperations.GetUntrackedFiles(repository, Files, ProjectDirectory);
        }
    }
}
