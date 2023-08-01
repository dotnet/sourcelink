// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed class GitRepository
    {
        private const int SupportedGitRepoFormatVersion = 1;

        private const string CommonDirFileName = "commondir";
        private const string GitDirName = ".git";
        private const string GitDirPrefix = "gitdir: ";

        // See https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-HEAD
        internal const string GitHeadFileName = "HEAD";

        private const string GitModulesFileName = ".gitmodules";

        private static readonly ImmutableArray<string> s_knownExtensions =
            ImmutableArray.Create("noop", "preciousObjects", "partialclone", "worktreeConfig");

        public GitConfig Config { get; }

        public GitIgnore Ignore => _lazyIgnore.Value;

        /// <summary>
        /// Normalized full path. OS specific directory separators.
        /// </summary>
        public string GitDirectory { get; }

        /// <summary>
        /// Normalized full path. OS specific directory separators.
        /// </summary>
        public string CommonDirectory { get; }

        /// <summary>
        /// Normalized full path. OS specific directory separators. Optional.
        /// </summary>
        public string? WorkingDirectory { get; }

        public GitEnvironment Environment { get; }

        private readonly Lazy<(ImmutableArray<GitSubmodule> Submodules, ImmutableArray<string> Diagnostics)> _lazySubmodules;
        private readonly Lazy<GitIgnore> _lazyIgnore;
        private readonly Lazy<string?> _lazyHeadCommitSha;
        private readonly GitReferenceResolver _referenceResolver;

        internal GitRepository(GitEnvironment environment, GitConfig config, string gitDirectory, string commonDirectory, string? workingDirectory)
        {
            NullableDebug.Assert(environment != null);
            NullableDebug.Assert(config != null);
            NullableDebug.Assert(PathUtils.IsNormalized(gitDirectory));
            NullableDebug.Assert(PathUtils.IsNormalized(commonDirectory));
            NullableDebug.Assert(workingDirectory == null || PathUtils.IsNormalized(workingDirectory));

            Config = config;
            GitDirectory = gitDirectory;
            CommonDirectory = commonDirectory;
            WorkingDirectory = workingDirectory;
            Environment = environment;

            _referenceResolver = new GitReferenceResolver(gitDirectory, commonDirectory);
            _lazySubmodules = new Lazy<(ImmutableArray<GitSubmodule>, ImmutableArray<string>)>(ReadSubmodules);
            _lazyIgnore = new Lazy<GitIgnore>(LoadIgnore);
            _lazyHeadCommitSha = new Lazy<string?>(ReadHeadCommitSha);
        }

        // test only
        internal GitRepository(
            GitEnvironment environment,
            GitConfig config,
            string gitDirectory,
            string commonDirectory,
            string? workingDirectory,
            ImmutableArray<GitSubmodule> submodules,
            ImmutableArray<string> submoduleDiagnostics,
            GitIgnore ignore,
            string? headCommitSha)
            : this(environment, config, gitDirectory, commonDirectory, workingDirectory)
        {
            _lazySubmodules = new Lazy<(ImmutableArray<GitSubmodule>, ImmutableArray<string>)>(() => (submodules, submoduleDiagnostics));
            _lazyIgnore = new Lazy<GitIgnore>(() => ignore);
            _lazyHeadCommitSha = new Lazy<string?>(() => headCommitSha);
        }        

        /// <summary>
        /// Opens a repository at the specified location.
        /// </summary>
        /// <exception cref="IOException" />
        /// <exception cref="InvalidDataException" />
        /// <exception cref="NotSupportedException">The repository found requires higher version of git repository format that is currently supported.</exception>
        /// <returns>null if no git repository can be found that contains the specified path.</returns>
        internal static GitRepository? OpenRepository(string path, GitEnvironment environment)
            => TryFindRepository(path, out var location) ? OpenRepository(location, environment) : null;

        /// <summary>
        /// Opens a repository at the specified location.
        /// </summary>
        /// <exception cref="IOException" />
        /// <exception cref="InvalidDataException" />
        /// <exception cref="NotSupportedException">The repository found requires higher version of git repository format that is currently supported.</exception>
        public static GitRepository OpenRepository(GitRepositoryLocation location, GitEnvironment environment)
        {
            NullableDebug.Assert(environment != null);
            NullableDebug.Assert(location.GitDirectory != null);
            NullableDebug.Assert(location.CommonDirectory != null);

            // See https://git-scm.com/docs/gitrepository-layout

            var reader = new GitConfig.Reader(location.GitDirectory, location.CommonDirectory, environment);
            var config = reader.Load();

            var workingDirectory = GetWorkingDirectory(config, location);

            // See https://github.com/git/git/blob/master/Documentation/technical/repository-version.txt
            string? versionStr = config.GetVariableValue("core", "repositoryformatversion");
            if (GitConfig.TryParseInt64Value(versionStr, out var version) && version > SupportedGitRepoFormatVersion)
            {
                throw new NotSupportedException(string.Format(Resources.UnsupportedRepositoryVersion, versionStr, SupportedGitRepoFormatVersion));
            }

            if (version == 1)
            {
                // All variables defined under extensions section must be known, otherwise a git implementation is not allowed to proced.
                foreach (var variable in config.Variables)
                {
                    if (variable.Key.SectionNameEquals("extensions") && !s_knownExtensions.Contains(variable.Key.VariableName, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException(string.Format(
                            Resources.UnsupportedRepositoryExtension, variable.Key.VariableName, string.Join(", ", s_knownExtensions)));
                    }
                }
            }

            return new GitRepository(environment, config, location.GitDirectory, location.CommonDirectory, workingDirectory);
        }

        private static string? GetWorkingDirectory(GitConfig config, GitRepositoryLocation location)
        {
            // TODO (https://github.com/dotnet/sourcelink/issues/301):
            // GIT_WORK_TREE environment variable can also override working directory.

            // Working directory can be overridden by a config option.
            // See https://git-scm.com/docs/git-config#Documentation/git-config.txt-coreworktree
            string? value = config.GetVariableValue("core", "worktree");
            if (value != null)
            {
                // git does not expand home dir relative path ("~/")
                try
                {
                    return Path.GetFullPath(Path.Combine(location.GitDirectory, value));
                }
                catch
                {
                    throw new InvalidDataException(string.Format(Resources.ValueOfIsNotValidPath, "core.worktree", value));
                }
            }

            return location.WorkingDirectory;
        }

        /// <summary>
        /// Returns the commit SHA of the current HEAD tip.
        /// </summary>
        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <returns>Null if the HEAD tip reference can't be resolved.</returns>
        public string? GetHeadCommitSha()
            => _lazyHeadCommitSha.Value;

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        private string? ReadHeadCommitSha()
            => _referenceResolver.ResolveHeadReference();

        /// <summary>
        /// Creates <see cref="GitReferenceResolver"/> for a submodule located in the specified <paramref name="submoduleWorkingDirectoryFullPath"/>.
        /// </summary>
        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <returns>Null if the submodule can't be located.</returns>
        public static GitReferenceResolver? GetSubmoduleReferenceResolver(string submoduleWorkingDirectoryFullPath)
        {
            // Submodules don't usually have their own .git directories but this is still legal.
            // This can occur with older versions of Git or other tools, or when a user clones one
            // repo into another's source tree (but it was not yet registered as a submodule).
            // See https://git-scm.com/docs/gitsubmodules#_forms for more details.
            var dotGitPath = Path.Combine(submoduleWorkingDirectoryFullPath, GitDirName);

            var gitDirectory =
                Directory.Exists(dotGitPath) ? dotGitPath :
                File.Exists(dotGitPath) ? ReadDotGitFile(dotGitPath) : null;

            if (gitDirectory == null || !IsGitDirectory(gitDirectory, out var commonDirectory))
            {
                return null;
            }

            return new GitReferenceResolver(gitDirectory, commonDirectory);
        }

        private string GetWorkingDirectory()
            => WorkingDirectory ?? throw new InvalidOperationException(Resources.RepositoryDoesNotHaveWorkingDirectory);

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <exception cref="NotSupportedException"/>
        public ImmutableArray<GitSubmodule> GetSubmodules()
            => _lazySubmodules.Value.Submodules;

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <exception cref="NotSupportedException"/>
        public ImmutableArray<string> GetSubmoduleDiagnostics()
            => _lazySubmodules.Value.Diagnostics;

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <exception cref="NotSupportedException"/>
        private (ImmutableArray<GitSubmodule> Submodules, ImmutableArray<string> Diagnostics) ReadSubmodules()
        {
            var workingDirectory = GetWorkingDirectory();
            var submoduleConfig = ReadSubmoduleConfig();
            if (submoduleConfig == null)
            {
                return (ImmutableArray<GitSubmodule>.Empty, ImmutableArray<string>.Empty);
            }

            ImmutableArray<string>.Builder? lazyDiagnostics = null;

            void reportDiagnostic(string diagnostic)
                => (lazyDiagnostics ??= ImmutableArray.CreateBuilder<string>()).Add(diagnostic);

            var builder = ImmutableArray.CreateBuilder<GitSubmodule>();

            foreach (var (name, path, url) in EnumerateSubmoduleConfig(submoduleConfig))
            {
                if (NullableString.IsNullOrWhiteSpace(path))
                {
                    reportDiagnostic(string.Format(Resources.InvalidSubmodulePath, name, path));
                    continue;
                }

                // Ignore unspecified URL - Source Link doesn't use it.

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

                string? headCommitSha;
                try
                {
                    var resolver = GetSubmoduleReferenceResolver(fullPath);
                    if (resolver == null)
                    {
                        // If we can't locate the submodule directory then it won't have any source files
                        // and we can safely ignore the submodule.
                        continue;
                    }

                    headCommitSha = resolver.ResolveHeadReference();
                }
                catch (Exception e) when (e is IOException or InvalidDataException)
                {
                    reportDiagnostic(e.Message);
                    continue;
                }

                builder.Add(new GitSubmodule(name, path, fullPath, url, headCommitSha));
            }

            return (builder.ToImmutable(), (lazyDiagnostics != null) ? lazyDiagnostics.ToImmutable() : ImmutableArray<string>.Empty);
        }

        // internal for testing
        internal GitConfig? ReadSubmoduleConfig()
        {
            var workingDirectory = GetWorkingDirectory();
            var submodulesConfigFile = Path.Combine(workingDirectory, GitModulesFileName);
            if (!File.Exists(submodulesConfigFile))
            {
                return null;
            }

            var reader = new GitConfig.Reader(GitDirectory, CommonDirectory, Environment);
            return reader.LoadFrom(submodulesConfigFile);
        }

        // internal for testing
        internal static IEnumerable<(string Name, string? Path, string? Url)> EnumerateSubmoduleConfig(GitConfig submoduleConfig)
        {
            foreach (var group in submoduleConfig.Variables.
                Where(kvp => kvp.Key.SectionNameEquals("submodule")).
                GroupBy(kvp => kvp.Key.SubsectionName, GitVariableName.SubsectionNameComparer).
                OrderBy(group => group.Key))
            {
                string name = group.Key;
                string? url = null;
                string? path = null;

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

                yield return (name, path, url);
            }
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

        /// <summary>
        /// Returns <see cref="GitRepositoryLocation"/> if the specified <paramref name="repositoryDirectory"/> is 
        /// a valid repository directory.
        /// </summary>
        /// <exception cref="IOException" />
        /// <exception cref="InvalidDataException" />
        /// <exception cref="NotSupportedException">The repository found requires higher version of git repository format that is currently supported.</exception>
        /// <returns>False if no git repository can be found that contains the specified path.</returns>
        public static bool TryGetRepositoryLocation(string directory, out GitRepositoryLocation location)
        {
            try
            {
                directory = Path.GetFullPath(directory);
            }
            catch
            {
                location = default;
                return false;
            }

            return TryGetRepositoryLocationImpl(directory, out location);
        }

        /// <summary>
        /// Finds a git repository containing the specified path, if any.
        /// </summary>
        /// <exception cref="IOException" />
        /// <exception cref="InvalidDataException" />
        /// <exception cref="NotSupportedException">The repository found requires higher version of git repository format that is currently supported.</exception>
        /// <returns>False if no git repository can be found that contains the specified path.</returns>
        public static bool TryFindRepository(string directory, out GitRepositoryLocation location)
        {
            var dir = directory;
            try
            {
                dir = Path.GetFullPath(dir);
            }
            catch
            {
                location = default;
                return false;
            }

            while (dir != null)
            {
                if (TryGetRepositoryLocationImpl(dir, out location))
                {
                    return true;
                }

                // TODO: https://github.com/dotnet/sourcelink/issues/302
                // stop on device boundary
                dir = Path.GetDirectoryName(dir);
            }

            location = default;
            return false;
        }

        private static bool TryGetRepositoryLocationImpl(string directory, out GitRepositoryLocation location)
        {
            string? commonDirectory;
            var dotGitPath = Path.Combine(directory, GitDirName);

            if (Directory.Exists(dotGitPath))
            {
                if (IsGitDirectory(dotGitPath, out commonDirectory))
                {
                    location = new GitRepositoryLocation(gitDirectory: dotGitPath, commonDirectory, workingDirectory: directory);
                    return true;
                }
            }
            else if (File.Exists(dotGitPath))
            {
                var link = ReadDotGitFile(dotGitPath);
                if (IsGitDirectory(link, out commonDirectory))
                {
                    location = new GitRepositoryLocation(gitDirectory: link, commonDirectory, workingDirectory: directory);
                    return true;
                }

                location = default;
                return false;
            }

            if (Directory.Exists(directory))
            {
                if (IsGitDirectory(directory, out commonDirectory))
                {
                    location = new GitRepositoryLocation(gitDirectory: directory, commonDirectory, workingDirectory: null);
                    return true;
                }
            }

            location = default;
            return false;
        }

        private static string ReadDotGitFile(string path)
        {
            string content;
            try
            {
                content = File.ReadAllText(path);
            }
            catch (Exception e) when (e is not IOException)
            {
                throw new IOException(e.Message, e);
            }

            if (!content.StartsWith(GitDirPrefix))
            {
                throw new InvalidDataException(string.Format(Resources.FormatOfFileIsInvalid, path));
            }

            var link = content.Substring(GitDirPrefix.Length).TrimEnd(CharUtils.AsciiWhitespace);

            try
            {
                // link is relative to the directory containing the file:
                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, link));
            }
            catch
            {
                throw new InvalidDataException(string.Format(Resources.PathSpecifiedInFileIsInvalid, path, link));
            }
        }

        private static bool IsGitDirectory(string directory, [NotNullWhen(true)]out string? commonDirectory)
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
                    commonDirectory = Path.Combine(directory, File.ReadAllText(commonLinkPath).TrimEnd(CharUtils.AsciiWhitespace));
                    // Normalize relative paths. For example, git worktrees typically have "../.." in this file.
                    commonDirectory = Path.GetFullPath(commonDirectory);
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
