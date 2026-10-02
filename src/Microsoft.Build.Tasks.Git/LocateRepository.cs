// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    [MSBuildMultiThreadableTask]
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

        /// <summary>
        /// Commit timestamp in UTC RFC3339 format.
        /// </summary>
        [Output]
        public string? RevisionTimestamp { get; private set; }

        /// <summary>
        /// Branch name.
        /// </summary>
        [Output]
        public string? BranchName { get; private set; }

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
            RevisionTimestamp = GetRevisionTimestamp(repository, RevisionId);
            BranchName = repository.GetBranchName();
        }

        private static string? GetRevisionTimestamp(GitRepository repository, string? revisionId)
        {
            if (revisionId == null)
            {
                return null;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"--no-pager show -s --format=%cI {revisionId}",
                    WorkingDirectory = repository.WorkingDirectory ?? repository.GitDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };

                startInfo.EnvironmentVariables["GIT_DIR"] = repository.GitDirectory;
                if (repository.WorkingDirectory != null)
                {
                    startInfo.EnvironmentVariables["GIT_WORK_TREE"] = repository.WorkingDirectory;
                }

                startInfo.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";

                using var process = Process.Start(startInfo);
                if (process == null)
                {
                    return null;
                }

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode != 0 ||
                    !DateTimeOffset.TryParse(output, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
                {
                    return null;
                }

                return timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
            }
            catch (Exception e) when (e is Win32Exception or InvalidOperationException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
