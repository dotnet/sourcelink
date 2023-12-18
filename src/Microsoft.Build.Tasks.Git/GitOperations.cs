// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
        private const string SubmoduleSectionName = "submodule";
        private const string RemoteOriginName = "origin";
        private const string UrlSectionName = "url";
        private const string UrlVariableName = "url";

        public static string? GetRepositoryUrl(GitRepository repository, string? remoteName, bool warnOnMissingOrUnsupportedRemote = true, Action<string, object?[]>? logWarning = null)
        {
            NullableDebug.Assert(repository.WorkingDirectory != null);

            var remoteUrl = GetRemoteUrl(repository, ref remoteName, warnOnMissingOrUnsupportedRemote, logWarning);
            if (remoteUrl == null)
            {
                return null;
            }

            var uri = NormalizeUrl(repository, remoteUrl);
            if (uri == null)
            {
                logWarning?.Invoke(Resources.InvalidRepositoryRemoteUrl, new[] { remoteName, remoteUrl });
                return null;
            }

            if (!IsSupportedScheme(uri.Scheme))
            {
                if (warnOnMissingOrUnsupportedRemote)
                {
                    // TODO: Better message https://github.com/dotnet/sourcelink/issues/1149
                    logWarning?.Invoke(Resources.InvalidRepositoryRemoteUrl, new[] { remoteName, remoteUrl });
                }

                return null;
            }

            return uri.AbsoluteUri;
        }

        private static string? GetRemoteUrl(GitRepository repository, ref string? remoteName, bool warnOnMissingRemote, Action<string, object?[]>? logWarning)
        {
            string? unknownRemoteName = null;
            string? remoteUrl = null;
            if (!NullableString.IsNullOrEmpty(remoteName))
            {
                remoteUrl = repository.Config.GetVariableValue(RemoteSectionName, remoteName, UrlVariableName);
                if (remoteUrl == null)
                {
                    unknownRemoteName = remoteName;
                }
            }

            if (remoteUrl == null && !TryGetRemote(repository.Config, out remoteName, out remoteUrl))
            {
                if (warnOnMissingRemote)
                {
                    logWarning?.Invoke(Resources.RepositoryHasNoRemote, new[] { repository.WorkingDirectory });
                }

                return null;
            }

            if (unknownRemoteName != null)
            {
                logWarning?.Invoke(Resources.RepositoryDoesNotHaveSpecifiedRemote, new[] { repository.WorkingDirectory, unknownRemoteName, remoteName });
            }

            return remoteUrl;
        }

        private static bool TryGetRemote(GitConfig config, [NotNullWhen(true)]out string? remoteName, [NotNullWhen(true)]out string? remoteUrl)
        {
            remoteName = RemoteOriginName;
            remoteUrl = config.GetVariableValue(RemoteSectionName, remoteName, UrlVariableName);
            if (remoteUrl != null)
            {
                return true;
            }

            var remoteVariable = config.Variables.
                Where(kvp => kvp.Key.SectionNameEquals(RemoteSectionName) && kvp.Key.VariableNameEquals(UrlVariableName)).
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
            string? replacement = null;

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

        internal static Uri? NormalizeUrl(GitRepository repository, string url)
        {
            // Git (v2.23.0) treats local relative URLs as relative to the working directory.
            // This doesn't work when a relative URL is used in a config file locatede in a main .git directory 
            // but is resolved from a worktree that has a different working directory.
            // Currently we implement the same behavior as git.

            NullableDebug.Assert(repository.WorkingDirectory != null);
            return NormalizeUrl(ApplyInsteadOfUrlMapping(repository.Config, url), root: repository.WorkingDirectory);
        }

        /// <summary>
        /// Git supports "http(s)", "ssh", "git" and "file" schemes. "ftp" support is obsolete. The scheme is case-sensitive.
        /// See https://git-scm.com/docs/git-clone#_git_urls.
        /// 
        /// Source Link does not support "file" scheme since repositories hosted locally or on a network share 
        /// are usually bare and the actual source files are not available (a work tree is not available).
        /// The debugger also does not support "file" URLs in Source Link.
        /// 
        /// The scenario of a repository on a network share that is not bare is rare since pushing into such repository
        /// is not allowed by default (see configuration "receive.denyCurrentBranch") and the work tree needs
        /// to be kept up to date by a service.
        /// </summary>
        private static bool IsSupportedScheme(string scheme)
            => scheme is "http" or "https" or "ssh" or "git";

        internal static Uri? NormalizeUrl(string url, string root)
        {
            // Since git supports scp-like syntax for SSH URLs we convert it here, 
            // so that RepositoryUrl is actually a valid URL in that case.
            // See https://git-scm.com/book/en/v2/Git-on-the-Server-The-Protocols.

            // Windows device path "X:"
            if (url.Length == 2 && IsWindowsAbsoluteOrDriveRelativePath(url))
            {
                return new Uri("file:///" + url + "/");
            }

            if (TryParseScp(url, out var uri))
            {
                return uri;
            }

            // Note that creating a Uri from a local path with certain Unicode characters
            // has issues (https://github.com/dotnet/runtime/issues/89538). 
            // However, we do not support file:// URIs, so if the actual URI
            // value doesn't matter for local paths.
            if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out uri))
            {
                return null;
            }

            if (uri.IsAbsoluteUri)
            {
                return uri;
            }

            // Convert relative local path to absolute:
            var rootUri = new Uri(root.EndWithSeparator('/'));
            if (Uri.TryCreate(rootUri, uri, out uri))
            {
                return uri;
            }

            return null;
        }

        private static bool IsWindowsAbsoluteOrDriveRelativePath(string value)
            => Path.DirectorySeparatorChar == '\\' &&
               value.Length >= 2 &&
               value[1] == ':' &&
               (value[0] >= 'A' && value[0] <= 'Z' || value[0] >= 'a' && value[0] <= 'z');

        private static bool TryParseScp(string value, [NotNullWhen(true)]out Uri? uri)
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

        public static ITaskItem[] GetSourceRoots(GitRepository repository, string? remoteName, bool warnOnMissingCommitOrUnsupportedUri, Action<string, object?[]> logWarning)
        {
            // Not supported for repositories without a working directory.
            NullableDebug.Assert(repository.WorkingDirectory != null);

            var result = new List<TaskItem>();
            var repoRoot = repository.WorkingDirectory.EndWithSeparator();

            var revisionId = repository.GetHeadCommitSha();
            if (revisionId != null)
            {
                // Don't report a warning since it has already been reported by GetRepositoryUrl task.
                string? repositoryUrl = GetRepositoryUrl(repository, remoteName, logWarning: null);

                // Item metadata are stored msbuild-escaped. GetMetadata unescapes, SetMetadata stores the value as specified.
                // Escape msbuild special characters so that URL escapes in the URL are preserved when the URL is read by GetMetadata.

                var item = new TaskItem(Evaluation.ProjectCollection.Escape(repoRoot));
                item.SetMetadata(Names.SourceRoot.SourceControl, SourceControlName);
                item.SetMetadata(Names.SourceRoot.ScmRepositoryUrl, Evaluation.ProjectCollection.Escape(repositoryUrl));
                item.SetMetadata(Names.SourceRoot.RevisionId, revisionId);
                result.Add(item);
            }
            else if (warnOnMissingCommitOrUnsupportedUri)
            {
                logWarning(Resources.RepositoryHasNoCommit, Array.Empty<object>());
            }

            foreach (var submodule in repository.GetSubmodules())
            {
                var commitSha = submodule.HeadCommitSha;
                if (commitSha == null)
                {
                    if (warnOnMissingCommitOrUnsupportedUri)
                    {
                        logWarning(Resources.SourceCodeWontBeAvailableViaSourceLink,
                            new[] { string.Format(Resources.SubmoduleWithoutCommit, new[] { submodule.Name }) });
                    }

                    continue;
                }

                // submodule.<name>.url specifies where to find the submodule.
                // This variable is calculated based on the entry in .gitmodules by git submodule init and will be present for initialized submodules.
                // Uninitialized modules don't have source that should be considered during the build.
                // Relative URLs are relative to the repository directory.
                // See https://git-scm.com/docs/gitsubmodules.
                var submoduleConfigUrl = repository.Config.GetVariableValue(SubmoduleSectionName, submodule.Name, UrlVariableName);
                if (submoduleConfigUrl == null)
                {
                    continue;
                }

                var submoduleUri = NormalizeUrl(repository, submoduleConfigUrl);
                if (submoduleUri == null)
                {
                    logWarning(Resources.SourceCodeWontBeAvailableViaSourceLink,
                        new[] { string.Format(Resources.InvalidSubmoduleUrl, submodule.Name, submoduleConfigUrl) });

                    continue;
                }

                if (!IsSupportedScheme(submoduleUri.Scheme))
                {
                    if (warnOnMissingCommitOrUnsupportedUri)
                    {
                        // TODO: Better message https://github.com/dotnet/sourcelink/issues/1149
                        logWarning(Resources.SourceCodeWontBeAvailableViaSourceLink,
                            new[] { string.Format(Resources.InvalidSubmoduleUrl, submodule.Name, submoduleConfigUrl) });
                    }

                    continue;
                }

                // Item metadata are stored msbuild-escaped. GetMetadata unescapes, SetMetadata stores the value as specified.
                // Escape msbuild special characters so that URL escapes and non-ascii characters in the URL and paths are 
                // preserved when read by GetMetadata.

                var item = new TaskItem(Evaluation.ProjectCollection.Escape(submodule.WorkingDirectoryFullPath.EndWithSeparator()));
                item.SetMetadata(Names.SourceRoot.SourceControl, SourceControlName);
                item.SetMetadata(Names.SourceRoot.ScmRepositoryUrl, Evaluation.ProjectCollection.Escape(submoduleUri.AbsoluteUri));
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

        public static ITaskItem[] GetUntrackedFiles(GitRepository repository, ITaskItem[] files, string projectDirectory)
            => GetUntrackedFiles(repository, files, projectDirectory, CreateSubmoduleRepository);

        private static GitRepository? CreateSubmoduleRepository(GitEnvironment environment, string directoryFullPath)
            => GitRepository.TryGetRepositoryLocation(directoryFullPath, out var location) ?
               GitRepository.OpenRepository(location, environment) : null;

        // internal for testing
        internal static ITaskItem[] GetUntrackedFiles(GitRepository repository, ITaskItem[] files, string projectDirectory, Func<GitEnvironment, string, GitRepository?> repositoryFactory)
        {
            var directoryTree = BuildDirectoryTree(repository, repositoryFactory);

            return files.Where(file =>
            {
                // file.ItemSpec are relative to projectDirectory.
                var fullPath = Path.GetFullPath(Path.Combine(projectDirectory, file.ItemSpec));

                var containingDirectoryMatcher = GetContainingRepositoryMatcher(fullPath, directoryTree);

                // Files that are outside of the repository are considered untracked.
                return containingDirectoryMatcher?.IsNormalizedFilePathIgnored(fullPath) ?? true;
            }).ToArray();
        }

        internal sealed class DirectoryNode
        {
            public readonly string Name;
            public readonly List<DirectoryNode> OrderedChildren;

            // set on nodes that represent working directory of the repository or a submodule:
            public Lazy<GitIgnore.Matcher?>? Matcher;

            public DirectoryNode(string name, List<DirectoryNode> orderedChildren)
            {
                Name = name;
                OrderedChildren = orderedChildren;
            }

            public int FindChildIndex(string name)
                => BinarySearch(OrderedChildren, name, (n, v) => n.Name.CompareTo(v));
        }

        internal static DirectoryNode BuildDirectoryTree(GitRepository repository, Func<GitEnvironment, string, GitRepository?> repositoryFactory)
        {
            NullableDebug.Assert(repository.WorkingDirectory != null);

            var treeRoot = new DirectoryNode(name: "", new List<DirectoryNode>());
            AddTreeNode(treeRoot, repository.WorkingDirectory, new Lazy<GitIgnore.Matcher?>(() => repository.Ignore.CreateMatcher()));

            foreach (var submodule in repository.GetSubmodules())
            {
                var submoduleWorkingDirectory = submodule.WorkingDirectoryFullPath;

                AddTreeNode(treeRoot, submoduleWorkingDirectory,
                    new Lazy<GitIgnore.Matcher?>(() => repositoryFactory(repository.Environment, submoduleWorkingDirectory)?.Ignore.CreateMatcher()));
            }

            return treeRoot;
        }

        private static void AddTreeNode(DirectoryNode root, string workingDirectory, Lazy<GitIgnore.Matcher?> matcher)
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
                    var newNode = new DirectoryNode(segments[i], new List<DirectoryNode>());
                    node.OrderedChildren.Insert(~index, newNode);
                    node = newNode;
                }

                if (i == segments.Length - 1)
                {
                    node.Matcher = matcher;
                }
            }
        }

        // internal for testing
        internal static GitIgnore.Matcher? GetContainingRepositoryMatcher(string fullPath, DirectoryNode root)
        {
            var segments = PathUtilities.Split(fullPath);
            Debug.Assert(segments.Length > 0);

            Debug.Assert(root.Matcher == null);
            GitIgnore.Matcher? containingRepositoryMatcher = null;

            var node = root;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                int index = node.FindChildIndex(segments[i]);
                if (index < 0)
                {
                    break;
                }

                node = node.OrderedChildren[index];

                var matcher = node.Matcher?.Value;
                if (matcher != null)
                {
                    // inner-most repository determines the ignore state of the file:
                    containingRepositoryMatcher = matcher;
                }
            }

            return containingRepositoryMatcher;
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
