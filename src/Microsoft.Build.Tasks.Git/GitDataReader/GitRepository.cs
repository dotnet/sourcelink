// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed class GitRepository
    {
        private readonly struct SubmoduleInfo
        {
            public readonly ImmutableArray<GitSubmodule> Submodules;
            public readonly ImmutableArray<string> Diagnostics;

            public SubmoduleInfo(ImmutableArray<GitSubmodule> submodules, ImmutableArray<string> diagnostics)
            {
                Debug.Assert(!submodules.IsDefault);
                Debug.Assert(!diagnostics.IsDefault);

                Submodules = submodules;
                Diagnostics = diagnostics;
            }
        }

        private const int SupportedGitRepoFormatVersion = 0;

        private const string CommonDirFileName = "commondir";
        private const string GitDirName = ".git";
        private const string GitDirPrefix = "gitdir: ";
        private const string GitDirFileName = "gitdir";

        // See https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-HEAD
        private const string GitHeadFileName = "HEAD";

        private const string GitModulesFileName = ".gitmodules";

        public GitConfig Config { get; }

        public GitIgnore Ignore => _gitIgnore.Value;

        /// <summary>
        /// Full path.
        /// </summary>
        public string GitDirectory { get; }

        /// <summary>
        /// Full path.
        /// </summary>
        public string CommonDirectory { get; }

        /// <summary>
        /// Optional, full path.
        /// </summary>
        public string WorkingDirectory { get; }

        public GitEnvironment Environment { get; }

        private readonly Lazy<SubmoduleInfo> _submodules;
        private readonly Lazy<GitIgnore> _gitIgnore;

        internal GitRepository(GitEnvironment environment, GitConfig config, string gitDirectory, string commonDirectory, string workingDirectory)
        {
            Debug.Assert(environment != null);
            Debug.Assert(config != null);
            Debug.Assert(gitDirectory != null);
            Debug.Assert(commonDirectory != null);

            Config = config;
            GitDirectory = gitDirectory;
            CommonDirectory = commonDirectory;
            WorkingDirectory = workingDirectory;
            Environment = environment;

            _submodules = new Lazy<SubmoduleInfo>(LoadSubmoduleConfiguration);
            _gitIgnore = new Lazy<GitIgnore>(LoadIgnore);
        }

        /// <summary>
        /// Finds a git repository contining the specified path, if any.
        /// </summary>
        /// <exception cref="IOException" />
        /// <exception cref="InvalidDataException" />
        /// <exception cref="NotSupportedException">The repository found requires higher version of git repository format that is currently supported.</exception>
        /// <returns>null if no git repository can be found that contains the specified path.</returns>
        public static GitRepository OpenRepository(string path, GitEnvironment environment)
        {
            Debug.Assert(path != null);
            Debug.Assert(environment != null);

            // See https://git-scm.com/docs/gitrepository-layout

            if (!LocateRepository(path, out var gitDirectory, out var commonDirectory, out var defaultWorkingDirectory))
            {
                // unable to find repository
                return null;
            }

            Debug.Assert(gitDirectory != null);
            Debug.Assert(commonDirectory != null);

            var reader = new GitConfig.Reader(gitDirectory, commonDirectory, environment);
            var config = reader.Load();

            var workingDirectory = GetWorkingDirectory(config, gitDirectory, commonDirectory) ?? defaultWorkingDirectory;

            // See https://github.com/git/git/blob/master/Documentation/technical/repository-version.txt
            string versionStr = config.GetVariableValue("core", "repositoryformatversion");
            if (GitConfig.TryParseInt64Value(versionStr, out var version) && version > SupportedGitRepoFormatVersion)
            {
                throw new NotSupportedException(string.Format(Resources.UnsupportedRepositoryVersion, versionStr, SupportedGitRepoFormatVersion));
            }

            return new GitRepository(environment, config, gitDirectory, commonDirectory, workingDirectory);
        }

        // internal for testing
        internal static string GetWorkingDirectory(GitConfig config, string gitDirectory, string commonDirectory)
        {
            // Working trees cannot have the same common directory and git directory.
            // 'gitdir' file in a git directory indicates a working tree.

            var gitdirFilePath = Path.Combine(gitDirectory, GitDirFileName);

            var isLinkedWorkingTree = PathUtils.ToPosixDirectoryPath(commonDirectory) != PathUtils.ToPosixDirectoryPath(gitDirectory) && 
                File.Exists(gitdirFilePath);

            if (isLinkedWorkingTree)
            {
                // https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-worktreesltidgtgitdir

                string workingDirectory;
                try
                {
                    workingDirectory = File.ReadAllText(gitdirFilePath);
                }
                catch (Exception e) when (!(e is IOException))
                {
                    throw new IOException(e.Message, e);
                }

                workingDirectory = workingDirectory.TrimEnd(CharUtils.AsciiWhitespace);

                // Path in gitdir file must be absolute.
                if (!PathUtils.IsAbsolute(workingDirectory))
                {
                    throw new InvalidDataException(string.Format(Resources.PathSpecifiedInFileIsNotAbsolute, gitdirFilePath));
                }

                try
                {
                    return Path.GetFullPath(workingDirectory);
                }
                catch
                {
                    throw new InvalidDataException(string.Format(Resources.PathSpecifiedInFileIsInvalid, gitdirFilePath));
                }
            }

            // See https://git-scm.com/docs/git-config#Documentation/git-config.txt-coreworktree
            string value = config.GetVariableValue("core", "worktree");
            if (value != null)
            {
                // git does not expand home dir relative path ("~/")
                try
                {
                    return Path.GetFullPath(Path.Combine(gitDirectory, value));
                }
                catch
                {
                    throw new InvalidDataException(string.Format(Resources.ValueOfIsNotValidPath, "core.worktree", value));
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the commit SHA of the current HEAD tip.
        /// </summary>
        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <returns>Null if the HEAD tip reference can't be resolved.</returns>
        public string GetHeadCommitSha()
            => GetHeadCommitSha(GitDirectory, CommonDirectory);

        /// <summary>
        /// Returns the commit SHA of the current HEAD tip of the specified submodule.
        /// </summary>
        /// <param name="submoduleWorkingDirectory">The path to the submodule working directory relative to the working directory of this repository.</param>
        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <returns>Null if the HEAD tip reference can't be resolved.</returns>
        public string GetSubmoduleHeadCommitSha(string submoduleWorkingDirectory)
        {
            Debug.Assert(submoduleWorkingDirectory != null);

            string dotGitPath;
            try
            {
                dotGitPath = Path.Combine(GetWorkingDirectory(), submoduleWorkingDirectory, GitDirName);
            }
            catch
            {
                throw new InvalidDataException(string.Format(Resources.InvalidModulePath, submoduleWorkingDirectory));
            }

            var gitDirectory = ReadDotGitFile(dotGitPath);
            if (!IsGitDirectory(gitDirectory, out var commonDirectory))
            {
                return null;
            }

            return GetHeadCommitSha(gitDirectory, commonDirectory);
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        private static string GetHeadCommitSha(string gitDirectory, string commonDirectory)
        {
            // See
            // https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-HEAD
            // https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-refs

            string headRef;
            try
            {
                headRef = File.ReadAllText(Path.Combine(gitDirectory, GitHeadFileName));
            }
            catch (Exception e) when (!(e is IOException))
            {
                throw new IOException(e.Message, e);
            }

            return ResolveReference(headRef, commonDirectory);
        }

        // internal for testing
        internal static string ResolveReference(string reference, string commonDirectory)
        {
            HashSet<string> lazyVisitedReferences = null;
            return ResolveReference(reference, commonDirectory, ref lazyVisitedReferences);
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        private static string ResolveReference(string reference, string commonDirectory, ref HashSet<string> lazyVisitedReferences)
        {
            const string refPrefix = "ref: ";
            if (reference.StartsWith(refPrefix + "refs/", StringComparison.Ordinal))
            {
                var symRef = reference.Substring(refPrefix.Length);

                if (lazyVisitedReferences != null && !lazyVisitedReferences.Add(symRef))
                {
                    // infinite recursion
                    throw new InvalidDataException(string.Format(Resources.RecursionDetectedWhileResolvingReference, reference));
                }

                string content;
                try
                {
                    content = File.ReadAllText(Path.Combine(commonDirectory, symRef));
                }
                catch (ArgumentException)
                {
                    throw new InvalidDataException(string.Format(Resources.InvalidReference, reference));
                }
                catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    return null;
                }
                catch (Exception e) when (!(e is IOException))
                {
                    throw new IOException(e.Message, e);
                }

                if (IsObjectId(reference))
                {
                    return reference;
                }

                lazyVisitedReferences ??= new HashSet<string>();

                return ResolveReference(content, commonDirectory, ref lazyVisitedReferences);
            }

            if (IsObjectId(reference))
            {
                return reference;
            }

            throw new InvalidDataException(string.Format(Resources.InvalidReference, reference));
        }

        private string GetWorkingDirectory()
            => WorkingDirectory ?? throw new InvalidOperationException(Resources.RepositoryDoesNotHaveWorkingDirectory);

        private static bool IsObjectId(string reference)
            => reference.Length == 40 && reference.All(CharUtils.IsHexadecimalDigit);

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        public ImmutableArray<GitSubmodule> GetSubmodules()
            => _submodules.Value.Submodules;

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        public ImmutableArray<string> GetSubmoduleDiagnostics()
            => _submodules.Value.Diagnostics;

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        private SubmoduleInfo LoadSubmoduleConfiguration()
        {
            var workingDirectory = GetWorkingDirectory();
            var submodulesConfigFile = Path.Combine(workingDirectory, GitModulesFileName);
            if (!File.Exists(submodulesConfigFile))
            {
                return new SubmoduleInfo(ImmutableArray<GitSubmodule>.Empty, ImmutableArray<string>.Empty);
            }

            ImmutableArray<string>.Builder lazyDiagnostics = null;

            void reportDiagnostic(string diagnostic)
                => (lazyDiagnostics ??= ImmutableArray.CreateBuilder<string>()).Add(diagnostic);

            var builder = ImmutableArray.CreateBuilder<GitSubmodule>();
            var reader = new GitConfig.Reader(GitDirectory, CommonDirectory, Environment);
            var submoduleConfig = reader.LoadFrom(submodulesConfigFile);

            foreach (var group in submoduleConfig.Variables.
                Where(kvp => kvp.Key.SectionNameEquals("submodule")).
                GroupBy(kvp => kvp.Key.SubsectionName, GitVariableName.SubsectionNameComparer).
                OrderBy(group => group.Key))
            {
                string name = group.Key;
                string url = null;
                string path = null;

                foreach (var variable in group)
                {
                    if (variable.Key.VariableNameEquals("path"))
                    {
                        path = variable.Value.Last();
                    }
                    else if (variable.Key.VariableNameEquals("url"))
                    {
                        url = variable.Value.Last();
                    }
                }

                if (string.IsNullOrWhiteSpace(path))
                {
                    reportDiagnostic(string.Format(Resources.InvalidSubmodulePath, name, path));
                }
                else if (string.IsNullOrWhiteSpace(url))
                {
                    reportDiagnostic(string.Format(Resources.InvalidSubmoduleUrl, name, url));
                }
                else
                {
                    string fullPath;
                    try
                    {
                        fullPath = Path.GetFullPath(Path.Combine(workingDirectory, path));
                    }
                    catch
                    {
                        reportDiagnostic(string.Format(Resources.InvalidSubmodulePath, name, path));
                        continue;
                    }

                    builder.Add(new GitSubmodule(name, path, fullPath, url));
                }
            }

            return new SubmoduleInfo(
                builder.ToImmutable(), 
                (lazyDiagnostics != null) ? lazyDiagnostics.ToImmutable() : ImmutableArray<string>.Empty);
        }

        private GitIgnore LoadIgnore()
        {
            var workingDirectory = GetWorkingDirectory();
            var ignoreCase = GitConfig.ParseBooleanValue(Config.GetVariableValue("core", "ignorecase"));
            var excludesFile = Config.GetVariableValue("core", "excludesFile");
            var commonInfoExclude = Path.Combine(CommonDirectory, "info", "exclude");

            var root = GitIgnore.LoadFromFile(commonInfoExclude, GitIgnore.LoadFromFile(excludesFile, parent: null));
            return new GitIgnore(root, workingDirectory, ignoreCase);
        }

        /// <exception cref="IOException" />
        /// <exception cref="InvalidDataException" />
        internal static bool LocateRepository(string directory, out string gitDirectory, out string commonDirectory, out string workingDirectory)
        {
            gitDirectory = commonDirectory = workingDirectory = null;

            try
            {
                directory = Path.GetFullPath(directory);
            }
            catch
            {
                return false;
            }

            while (directory != null)
            {
                // TODO: stop on device boundary
                
                var dotGitPath = Path.Combine(directory, GitDirName);

                if (Directory.Exists(dotGitPath))
                {
                    if (IsGitDirectory(dotGitPath, out commonDirectory))
                    {
                        gitDirectory = dotGitPath;
                        workingDirectory = directory;
                        return true;
                    }
                }
                else if (File.Exists(dotGitPath))
                {
                    var link = ReadDotGitFile(dotGitPath);
                    if (IsGitDirectory(link, out commonDirectory))
                    {
                        gitDirectory = link;
                        workingDirectory = directory;
                        return true;
                    }

                    return false;
                }

                if (Directory.Exists(directory))
                {
                    if (IsGitDirectory(directory, out commonDirectory))
                    {
                        gitDirectory = directory;
                        workingDirectory = null;
                        return true;
                    }
                }

                directory = Path.GetDirectoryName(directory);
            }

            return false;
        }

        private static string ReadDotGitFile(string path)
        {
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception e) when (!(e is IOException))
            {
                throw new IOException(e.Message, e);
            }

            if (!content.StartsWith(GitDirPrefix))
            {
                throw new InvalidDataException(string.Format(Resources.FormatOfFileIsInvalid, path));
            }

            // git does not trim whitespace:
            var link = content.Substring(GitDirPrefix.Length);

            try
            {
                // link is relative to the directory containing the file:
                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path), link));
            }
            catch
            {
                throw new InvalidDataException(string.Format(Resources.PathSpecifiedInFileIsInvalid, path));
            }
        }

        private static bool IsGitDirectory(string directory, out string commonDirectory)
        {
            // HEAD file is required
            if (!File.Exists(Path.Combine(directory, GitHeadFileName)))
            {
                commonDirectory = null;
                return false;
            }

            // Spec https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-commondir:
            var commonLinkPath = Path.Combine(directory, CommonDirFileName);
            if (File.Exists(commonLinkPath))
            {
                try
                {
                    // note: git does not trim whitespace
                    commonDirectory = Path.Combine(directory, File.ReadAllText(commonLinkPath));
                }
                catch
                {
                    // git does not consider the directory valid git directory if the content of commondir file is malformed
                    commonDirectory = null;
                    return false;
                }
            }
            else
            {
                commonDirectory = directory;
            }

            // Git also requires objects and refs directories, but we allow them to be missing.
            // See https://github.com/dotnet/sourcelink/tree/master/docs#minimal-git-repository-metadata
            return Directory.Exists(commonDirectory);
        }
    }
}
