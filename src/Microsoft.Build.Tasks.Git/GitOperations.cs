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

            return NormalizeUrl(remote.Url, repository.Info.WorkingDirectory);
        }

        internal static string NormalizeUrl(string url, string root)
        {
            // Since git supports scp-like syntax for SSH URLs we convert it here, 
            // so that RepositoryUrl is actually a valid URL in that case.
            // See https://git-scm.com/book/en/v2/Git-on-the-Server-The-Protocols and
            // https://github.com/libgit2/libgit2/blob/master/src/transport.c#L72.

            // Windows device path "X:"
            if (url.Length == 2 && IsWindowsAbsoluteOrDriveRelativePath(url))
            {
                return "file:///" + url + "/";
            }

            if (TryParseScp(url, out var uri))
            {
                return uri.ToString();
            }

            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
            {
                return null;
            }

            if (uri.IsAbsoluteUri)
            {
                return uri.ToString();
            }

            // Convert relative local path to absolute:
            var rootUri = new Uri(root.EndWithSeparator('/'));
            if (Uri.TryCreate(rootUri, uri, out uri))
            {
                return uri.ToString();
            }

            return null;
        }

        private static bool IsWindowsAbsoluteOrDriveRelativePath(string value)
            => Path.DirectorySeparatorChar == '\\' &&
               value.Length >= 2 &&
               value[1] == ':' &&
               (value[0] >= 'A' && value[0] <= 'Z' || value[0] >= 'a' && value[0] <= 'z');

        private static bool TryParseScp(string value, out Uri uri)
        {
            uri = null;
           
            int colon = value.IndexOf(':');
            if (colon == -1)
            {
                return false;
            }

            // URLs xxx://xxx
            if (colon + 2 < value.Length && value[colon + 1] == '/' && value[colon + 2] == '/')
            {
                return false;
            }

            // Windows absolute or driver-relative paths "X:\xxx", "X:xxx"
            if (IsWindowsAbsoluteOrDriveRelativePath(value))
            {
                return false;
            }

            // [user@]server:path
            int start = value.IndexOf('@', 0, colon) + 1;
            var url = "https://" + value.Substring(start, colon - start) + "/" + value.Substring(colon + 1);
            return Uri.TryCreate(url, UriKind.Absolute, out uri);
        }

        public static string GetRevisionId(this IRepository repository)
        {
            // An empty repository doesn't have a tip commit:
            return repository.Head.Tip?.Sha;
        }

        public static ITaskItem[] GetSourceRoots(this IRepository repository, Action<string, string[]> logWarning)
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
                if (commitId == null)
                {
                    logWarning("SubmoduleWithoutCommit_SourceLink", new[] { submodule.Name });
                    continue;
                }

                // https://git-scm.com/docs/git-submodule
                // <repository> is the URL of the new submodule's origin repository. This may be either an absolute URL, or (if it begins with ./ or ../), 
                // the location relative to the superproject's default remote repository (Please note that to specify a repository foo.git which is located 
                // right next to a superproject bar.git, you'll have to use ../foo.git instead of ./foo.git - as one might expect when following the rules 
                // for relative URLs -- because the evaluation of relative URLs in Git is identical to that of relative directories).
                //
                // The given URL is recorded into .gitmodules for use by subsequent users cloning the superproject. 
                // If the URL is given relative to the superproject's repository, the presumption is the superproject and submodule repositories
                // will be kept together in the same relative location, and only the superproject's URL needs to be provided.git --
                // submodule will correctly locate the submodule using the relative URL in .gitmodules.
                var submoduleUrl = NormalizeUrl(submodule.Url, repoRoot);
                if (submoduleUrl == null)
                {
                    logWarning("InvalidSubmoduleUrl_SourceLink", new[] { submodule.Name, submodule.Url });
                    continue;
                }

                string submoduleRoot;
                try
                {
                    submoduleRoot = Path.GetFullPath(Path.Combine(repoRoot, submodule.Path)).EndWithSeparator();
                }
                catch
                {
                    logWarning("InvalidSubmodulePath_SourceLink", new[] { submodule.Name, submodule.Path });
                    continue;
                }

                var item = new TaskItem(submoduleRoot);
                item.SetMetadata(Names.SourceRoot.SourceControl, SourceControlName);
                item.SetMetadata(Names.SourceRoot.RepositoryUrl, submoduleUrl);
                item.SetMetadata(Names.SourceRoot.RevisionId, commitId.Sha);
                item.SetMetadata(Names.SourceRoot.ContainingRoot, repoRoot);
                item.SetMetadata(Names.SourceRoot.NestedRoot, submodule.Path.EndWithSeparator('/'));
                result.Add(item);
            }

            return result.ToArray();
        }

        private static string GetRepositoryRoot(this IRepository repository)
        {
            Debug.Assert(!repository.Info.IsBare);
            return Path.GetFullPath(repository.Info.WorkingDirectory).EndWithSeparator();
        }

        public static ITaskItem[] GetUntrackedFiles(
            this IRepository repository,
            ITaskItem[] files, 
            string projectDirectory,
            Func<string, IRepository> repositoryFactory)
        {
            var directoryTree = BuildDirectoryTree(repository);

            return files.Where(file =>
            {
                // file.ItemSpec are relative to projectDirectory.
                var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, file.ItemSpec));

                var containingDirectory = GetContainingRepository(fullPath, directoryTree);

                // libgit API doesn't work with backslashes:
                return containingDirectory?.GetRepository(repositoryFactory).Ignore.IsPathIgnored(fullPath.Replace('\\', '/')) == true;
            }).ToArray();
        }

        internal sealed class SourceControlDirectory
        {
            public readonly string Name;
            public readonly List<SourceControlDirectory> OrderedChildren;

            public string RepositoryFullPath;
            private IRepository _lazyRepository;

            public SourceControlDirectory(string name)
                : this(name, null, new List<SourceControlDirectory>())
            {
            }

            public SourceControlDirectory(string name, string repositoryFullPath)
                : this(name, repositoryFullPath, new List<SourceControlDirectory>())
            {
            }

            public SourceControlDirectory(string name, string repositoryFullPath, List<SourceControlDirectory> orderedChildren)
            {
                Name = name;
                RepositoryFullPath = repositoryFullPath;
                OrderedChildren = orderedChildren;
            }

            public void SetRepository(string fullPath, IRepository repository)
            {
                RepositoryFullPath = fullPath;
                _lazyRepository = repository;
            }

            public int FindChildIndex(string name)
                => BinarySearch(OrderedChildren, name, (n, v) => n.Name.CompareTo(v));

            public IRepository GetRepository(Func<string, IRepository> repositoryFactory)
                => _lazyRepository ?? (_lazyRepository = repositoryFactory(RepositoryFullPath));
        }

        internal static SourceControlDirectory BuildDirectoryTree(IRepository repository)
        {
            var repoRoot = Path.GetFullPath(repository.Info.WorkingDirectory);

            var treeRoot = new SourceControlDirectory("");
            AddTreeNode(treeRoot, repoRoot, repository);

            foreach (var submodule in repository.Submodules)
            {
                string fullPath;

                try
                {
                    fullPath = Path.GetFullPath(Path.Combine(repoRoot, submodule.Path));
                }
                catch
                {
                    // ignore submodules with bad paths
                    continue;
                }

                AddTreeNode(treeRoot, fullPath, repositoryOpt: null);
            }

            return treeRoot;
        }

        private static void AddTreeNode(SourceControlDirectory root, string fullPath, IRepository repositoryOpt)
        {
            var segments = PathUtilities.Split(fullPath);

            var node = root;

            for (int i = 0; i < segments.Length; i++)
            {
                int index = node.FindChildIndex(segments[i]);
                if (index >= 0)
                {
                    node = node.OrderedChildren[index];
                }
                else
                {
                    var newNode = new SourceControlDirectory(segments[i]);
                    node.OrderedChildren.Insert(~index, newNode);
                    node = newNode;
                }

                if (i == segments.Length - 1)
                {
                    node.SetRepository(fullPath, repositoryOpt);
                }
            }
        }

        // internal for testing
        internal static SourceControlDirectory GetContainingRepository(string fullPath, SourceControlDirectory root)
        {
            var segments = PathUtilities.Split(fullPath);
            Debug.Assert(segments.Length > 0);

            Debug.Assert(root.RepositoryFullPath == null);
            SourceControlDirectory containingRepositoryNode = null;

            var node = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                int index = node.FindChildIndex(segments[i]);
                if (index < 0)
                {
                    break;
                }

                node = node.OrderedChildren[index];
                if (node.RepositoryFullPath != null)
                {
                    containingRepositoryNode = node;
                }
            }

            return containingRepositoryNode;
        }

        private static readonly SequenceComparer<string> SplitPathComparer =
             new SequenceComparer<string>(Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        internal static int BinarySearch<T, TValue>(IReadOnlyList<T> list, TValue value, Func<T, TValue, int> compare)
        {
            var low = 0;
            var high = list.Count - 1;

            while (low <= high)
            {
                var middle = low + ((high - low) >> 1);
                var midValue = list[middle];

                var comparison = compare(midValue, value);
                if (comparison == 0)
                {
                    return middle;
                }

                if (comparison > 0)
                {
                    high = middle - 1;
                }
                else
                {
                    low = middle + 1;
                }
            }

            return ~low;
        }
    }
}
