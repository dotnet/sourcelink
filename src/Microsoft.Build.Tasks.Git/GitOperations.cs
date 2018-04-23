// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LibGit2Sharp;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    internal static class GitOperations
    {
        private const string SourceControlName = "git";

        static GitOperations()
        {
            // .NET Core apps that depend on native libraries load them directly from paths specified
            // in .deps.json file of that app and the native library loader just works.
            // However, .NET Core currently doesn't support .deps.json for plugins such as msbuild tasks.
            if (IsRunningOnNetCore())
            {
                var dir = Path.GetDirectoryName(typeof(GitOperations).Assembly.Location);
                GlobalSettings.NativeLibraryPath = Path.Combine(dir, "runtimes", GetNativeLibraryRuntimeId(), "native");
            }
        }

        /// <summary>
        /// Returns true if the runtime is .NET Core.
        /// </summary>
        private static bool IsRunningOnNetCore()
            => typeof(object).Assembly.GetName().Name != "mscorlib";

        /// <summary>
        /// Determines the RID to use when loading libgit2 native library.
        /// This method only supports RIDs that are currently used by LibGit2Sharp.NativeBinaries.
        /// </summary>
        private static string GetNativeLibraryRuntimeId()
        {
            var processorArchitecture = IntPtr.Size == 8 ? "x64" : "x86";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win7-" + processorArchitecture;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux-" + processorArchitecture;
            }

            throw new PlatformNotSupportedException();
        }

        public static string LocateRepository(string directory)
        {
            // Repository.Discover returns the path to .git directory for repositories with a working directory.
            // For bare repositories it returns the repository directory.
            // Returns null if the path is invalid or no repository is found.
            return Repository.Discover(directory);
        }

        public static string GetRepositoryUrl(this IRepository repository, string remoteName = null)
        {
            var remotes = repository.Network.Remotes;
            var remote = string.IsNullOrEmpty(remoteName) ? (remotes["origin"] ?? remotes.FirstOrDefault()) : remotes[remoteName];
            if (remote == null)
            {
                return null;
            }

            // Note that the value of the URL is whatever the remote was set to (may be invalid).
            return remote.Url;
        }

        public static string GetRevisionId(this IRepository repository)
        {
            // An empty repository doesn't have a tip commit:
            return repository.Head.Tip?.Sha;
        }

        public static ITaskItem[] GetSourceRoots(this IRepository repository, Action<string, object[]> logWarning)
        {
            var result = new List<TaskItem>();
            var repoRoot = GetRepositoryRoot(repository);

            var revisionId = GetRevisionId(repository);
            if (revisionId != null)
            {
                var item = new TaskItem(repoRoot);
                item.SetMetadata(Names.SourceRoot.SourceControl, SourceControlName);
                item.SetMetadata(Names.SourceRoot.RepositoryUrl, GetRepositoryUrl(repository));
                item.SetMetadata(Names.SourceRoot.RevisionId, revisionId);
                result.Add(item);
            }
            else
            {
                logWarning("RepositoryWithoutCommit_SourceLink", Array.Empty<string>());
            }

            foreach (var submodule in repository.Submodules)
            {
                var commitId = submodule.WorkDirCommitId;
                if (commitId != null)
                {
                    var item = new TaskItem(Path.GetFullPath(Path.Combine(repoRoot, submodule.Path)).EndWithSeparator());
                    item.SetMetadata(Names.SourceRoot.SourceControl, SourceControlName);
                    item.SetMetadata(Names.SourceRoot.RepositoryUrl, submodule.Url);
                    item.SetMetadata(Names.SourceRoot.RevisionId, commitId.Sha);
                    item.SetMetadata(Names.SourceRoot.ContainingRoot, repoRoot);
                    item.SetMetadata(Names.SourceRoot.NestedRoot, submodule.Path.EndWithSeparator('/'));
                    result.Add(item);
                }
                else
                {
                    logWarning("SubmoduleWithoutCommit_SourceLink", new[] { submodule.Name });
                }
            }

            return result.ToArray();
        }

        private static string GetRepositoryRoot(this IRepository repository)
        {
            Debug.Assert(!repository.Info.IsBare);
            return Path.GetFullPath(repository.Info.WorkingDirectory).EndWithSeparator();
        }

        public static ITaskItem[] GetUntrackedFiles(this IRepository repository, ITaskItem[] files)
        {
            var repoRoot = GetRepositoryRoot(repository);

            var pathComparer = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

            // TODO: 
            // file.ItemSpec are relative to the project dir.
            // Does gitlib handle backslashes on Windows?
            // Consider using paths relative to the repo root to avoid GetFullPath.
            return files.Where(file =>
            {
                var fullPath = Path.GetFullPath(file.ItemSpec);
                return fullPath.StartsWith(repoRoot, pathComparer) && repository.Ignore.IsPathIgnored(fullPath);
            }).ToArray();
        }
    }
}
