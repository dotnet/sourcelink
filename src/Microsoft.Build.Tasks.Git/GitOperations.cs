// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    internal static class GitOperations
    {
        private const string SourceControlName = "git";
        private const string RemoteSectionName = "remote";
        private const string UrlSectionName = "url";

        public static string GetRepositoryUrl(GitRepository repository, Action<string, object[]> logWarning = null, string remoteName = null)
        {
            string unknownRemoteName = null;
            string remoteUrl = null;
            if (!string.IsNullOrEmpty(remoteName))
            {
                remoteUrl = repository.Config.GetVariableValue(RemoteSectionName, remoteName, "url");
                if (remoteUrl == null)
                {
                    unknownRemoteName = remoteName;
                }
            }

            if (remoteUrl == null && !TryGetRemote(repository.Config, out remoteName, out remoteUrl))
            {
                logWarning?.Invoke(Resources.RepositoryHasNoRemote, Array.Empty<string>());
                return null;
            }

            if (unknownRemoteName != null)
            {
                logWarning?.Invoke(Resources.RepositoryDoesNotHaveSpecifiedRemote, new[] { unknownRemoteName, remoteName });
            }

            var url = NormalizeUrl(repository.Config, remoteUrl, repository.WorkingDirectory);
            if (url == null)
            {
                logWarning?.Invoke(Resources.InvalidRepositoryRemoteUrl, new[] { remoteName, remoteUrl });
            }

            return url;
        }

        private static bool TryGetRemote(GitConfig config, out string remoteName, out string remoteUrl)
        {
            remoteName = "origin";
            remoteUrl = config.GetVariableValue(RemoteSectionName, remoteName, "url");
            if (remoteUrl != null)
            {
                return true;
            }

            var remoteVariable = config.Variables.
                Where(kvp => kvp.Key.SectionNameEquals(RemoteSectionName)).
                OrderBy(kvp => kvp.Key.SubsectionName, GitVariableName.SubsectionNameComparer).
                FirstOrDefault();

            remoteName = remoteVariable.Key.SubsectionName;
            if (remoteName == null)
            {
                return false;
            }

            remoteUrl = remoteVariable.Value.Last();
            return true;
        }

        internal static string ApplyInsteadOfUrlMapping(GitConfig config, string url)
        {
            // See https://git-scm.com/docs/git-config#Documentation/git-config.txt-urlltbasegtinsteadOf
            // Notes:
            //  - URL prefix matching is case sensitive.
            //  - if the replacement is empty the URL is prefixed with the replacement string

            int longestPrefixLength = -1;
            string replacement = null;

            foreach (var variable in config.Variables)
            {
                if (variable.Key.SectionNameEquals(UrlSectionName) && 
                    variable.Key.VariableNameEquals("insteadOf"))
                {
                    foreach (var prefix in variable.Value)
                    {
                        if (prefix.Length > longestPrefixLength && url.StartsWith(prefix, StringComparison.Ordinal))
                        {
                            longestPrefixLength = prefix.Length;
                            replacement = variable.Key.SubsectionName;
                        }
                    }
                }
            }

            return (longestPrefixLength >= 0) ? replacement + url.Substring(longestPrefixLength) : url;
        }

        internal static string NormalizeUrl(GitConfig config, string url, string root)
            => NormalizeUrl(ApplyInsteadOfUrlMapping(config, url), root);

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
            var url = "ssh://" + value.Substring(0, colon) + "/" + value.Substring(colon + 1);
            return Uri.TryCreate(url, UriKind.Absolute, out uri);
        }

        public static ITaskItem[] GetSourceRoots(GitRepository repository, Action<string, object[]> logWarning)
        {
            var result = new List<TaskItem>();
            var repoRoot = GetRepositoryRoot(repository);

            var revisionId = repository.GetHeadCommitSha();
            if (revisionId != null)
            {
                // Don't report a warning since it has already been reported by GetRepositoryUrl task.
                string repositoryUrl = GetRepositoryUrl(repository);

                // Item metadata are stored msbuild-escaped. GetMetadata unescapes, SetMetadata stores the value as specified.
                // Escape msbuild special characters so that URL escapes in the URL are preserved when the URL is read by GetMetadata.

                var item = new TaskItem(Evaluation.ProjectCollection.Escape(repoRoot));
                item.SetMetadata(Names.SourceRoot.SourceControl, SourceControlName);
                item.SetMetadata(Names.SourceRoot.ScmRepositoryUrl, Evaluation.ProjectCollection.Escape(repositoryUrl));
                item.SetMetadata(Names.SourceRoot.RevisionId, revisionId);
                result.Add(item);
            }
            else
            {
                logWarning(Resources.RepositoryHasNoCommit, Array.Empty<object>());
            }

            foreach (var submodule in repository.GetSubmodules())
            {
                var commitSha = submodule.HeadCommitSha;
                if (commitSha == null)
                {
                    logWarning(Resources.SourceCodeWontBeAvailableViaSourceLink, 
                        new[] { string.Format(Resources.SubmoduleWithoutCommit, new[] { submodule.Name }) });

                    continue;
                }

                // https://git-scm.com/docs/git-submodule
                var submoduleUrl = NormalizeUrl(repository.Config, submodule.Url, repoRoot);
                if (submoduleUrl == null)
                {
                    logWarning(Resources.SourceCodeWontBeAvailableViaSourceLink, 
                        new[] { string.Format(Resources.InvalidSubmoduleUrl, submodule.Name, submodule.Url) });

                    continue;
                }

                // Item metadata are stored msbuild-escaped. GetMetadata unescapes, SetMetadata stores the value as specified.
                // Escape msbuild special characters so that URL escapes and non-ascii characters in the URL and paths are 
                // preserved when read by GetMetadata.

                var item = new TaskItem(Evaluation.ProjectCollection.Escape(submodule.WorkingDirectoryFullPath.EndWithSeparator()));
                item.SetMetadata(Names.SourceRoot.SourceControl, SourceControlName);
                item.SetMetadata(Names.SourceRoot.ScmRepositoryUrl, Evaluation.ProjectCollection.Escape(submoduleUrl));
                item.SetMetadata(Names.SourceRoot.RevisionId, commitSha);
                item.SetMetadata(Names.SourceRoot.ContainingRoot, Evaluation.ProjectCollection.Escape(repoRoot));
                item.SetMetadata(Names.SourceRoot.NestedRoot, Evaluation.ProjectCollection.Escape(submodule.WorkingDirectoryRelativePath.EndWithSeparator('/')));
                result.Add(item);
            }

            foreach (var diagnostic in repository.GetSubmoduleDiagnostics())
            {
                logWarning(Resources.SourceCodeWontBeAvailableViaSourceLink, new[] { diagnostic });
            }

            return result.ToArray();
        }

        private static string GetRepositoryRoot(GitRepository repository)
            => repository.WorkingDirectory.EndWithSeparator();

        public static ITaskItem[] GetUntrackedFiles(
            GitRepository repository,
            ITaskItem[] files, 
            string projectDirectory,
            Func<string, GitRepository> repositoryFactory)
        {
            var directoryTree = BuildDirectoryTree(repository);

            return files.Where(file =>
            {
                // file.ItemSpec are relative to projectDirectory.
                var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, file.ItemSpec));

                var containingDirectory = GetContainingRepository(fullPath, directoryTree);

                // Files that are outside of the repository are considered untracked.
                if (containingDirectory == null)
                {
                    return true;
                }

                return containingDirectory.GetMatcher(repositoryFactory).IsNormalizedFilePathIgnored(fullPath) ?? true;
            }).ToArray();
        }

        internal sealed class DirectoryNode
        {
            public readonly string Name;
            public readonly List<DirectoryNode> OrderedChildren;

            // set on nodes that represent submodule working directory:
            public string WorkingDirectoryFullPath;
            private GitIgnore.Matcher _lazyMatcher;

            public DirectoryNode(string name)
                : this(name, null, new List<DirectoryNode>())
            {
            }

            public DirectoryNode(string name, string fullPath)
                : this(name, fullPath, new List<DirectoryNode>())
            {
            }

            public DirectoryNode(string name, string workingDirectoryFullPath, List<DirectoryNode> orderedChildren)
            {
                Name = name;
                WorkingDirectoryFullPath = workingDirectoryFullPath;
                OrderedChildren = orderedChildren;
            }

            public void SetMatcher(string workingDirectory, GitIgnore.Matcher matcher)
            {
                WorkingDirectoryFullPath = workingDirectory;
                _lazyMatcher = matcher;
            }

            public int FindChildIndex(string name)
                => BinarySearch(OrderedChildren, name, (n, v) => n.Name.CompareTo(v));

            public GitIgnore.Matcher GetMatcher(Func<string, GitRepository> repositoryFactory)
                => _lazyMatcher ?? (_lazyMatcher = repositoryFactory(WorkingDirectoryFullPath).Ignore.CreateMatcher());
        }

        internal static DirectoryNode BuildDirectoryTree(GitRepository repository)
        {
            var workingDirectory = repository.WorkingDirectory;

            var treeRoot = new DirectoryNode("");
            AddTreeNode(treeRoot, workingDirectory, repository.Ignore.CreateMatcher());

            foreach (var submodule in repository.GetSubmodules())
            {
                AddTreeNode(treeRoot, submodule.WorkingDirectoryFullPath, matcherOpt: null);
            }

            return treeRoot;
        }

        private static void AddTreeNode(DirectoryNode root, string workingDirectory, GitIgnore.Matcher matcherOpt)
        {
            var segments = PathUtilities.Split(workingDirectory);

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
                    var newNode = new DirectoryNode(segments[i]);
                    node.OrderedChildren.Insert(~index, newNode);
                    node = newNode;
                }

                if (i == segments.Length - 1)
                {
                    node.SetMatcher(workingDirectory, matcherOpt);
                }
            }
        }

        // internal for testing
        internal static DirectoryNode GetContainingRepository(string fullPath, DirectoryNode root)
        {
            var segments = PathUtilities.Split(fullPath);
            Debug.Assert(segments.Length > 0);

            Debug.Assert(root.WorkingDirectoryFullPath == null);
            DirectoryNode containingRepositoryNode = null;

            var node = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                int index = node.FindChildIndex(segments[i]);
                if (index < 0)
                {
                    break;
                }

                node = node.OrderedChildren[index];
                if (node.WorkingDirectoryFullPath != null)
                {
                    containingRepositoryNode = node;
                }
            }

            return containingRepositoryNode;
        }

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
