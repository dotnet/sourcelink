// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed class GitEnvironment
    {
        private const string LocalConfigurationScopeName = "local";
        private const string GitRepositoryConfigurationScopeName = "GitRepositoryConfigurationScope";

        public static readonly GitEnvironment Empty = new GitEnvironment();

        public string? HomeDirectory { get; }
        public string? XdgConfigHomeDirectory { get; }
        public string? ProgramDataDirectory { get; }
        public string? SystemDirectory { get; }

        // TODO: https://github.com/dotnet/sourcelink/issues/301
        // consider adding environment variables: GIT_DIR, GIT_DISCOVERY_ACROSS_FILESYSTEM, GIT_CEILING_DIRECTORIES
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITDIRcode
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITCEILINGDIRECTORIEScode
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITDISCOVERYACROSSFILESYSTEMcode
        //
        // if GIT_COMMON_DIR is set config worktree is ignored
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITCOMMONDIRcode
        // 
        // GIT_WORK_TREE overrides all other work tree settings:
        // https://git-scm.com/docs/git#Documentation/git.txt-codeGITWORKTREEcode

        public GitEnvironment(
            string? homeDirectory = null,
            string? xdgConfigHomeDirectory = null,
            string? programDataDirectory = null,
            string? systemDirectory = null)
        {
            if (!string.IsNullOrWhiteSpace(homeDirectory))
            {
                HomeDirectory = homeDirectory;
            }

            if (!string.IsNullOrWhiteSpace(xdgConfigHomeDirectory))
            {
                XdgConfigHomeDirectory = xdgConfigHomeDirectory;
            }

            if (!string.IsNullOrWhiteSpace(programDataDirectory))
            {
                ProgramDataDirectory = programDataDirectory;
            }

            if (!string.IsNullOrWhiteSpace(systemDirectory))
            {
                SystemDirectory = systemDirectory;
            }
        }

        public static GitEnvironment Create(string? configurationScope)
        {
            if (NullableString.IsNullOrEmpty(configurationScope))
            {
                return CreateFromProcessEnvironment();
            }

            if (string.Equals(configurationScope, LocalConfigurationScopeName, StringComparison.OrdinalIgnoreCase))
            {
                return Empty;
            }

            throw new NotSupportedException(string.Format(Resources.ValueOfIsNotValidConfigurationScope, GitRepositoryConfigurationScopeName, configurationScope));
        }

        public static GitEnvironment CreateFromProcessEnvironment()
        {
            static string? getVariable(string name)
            {
                try
                {
                    return Environment.GetEnvironmentVariable(name);
                }
                catch
                {
                    return null;
                }
            }

            string? homeDirectory;
            try
            {
                homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile, Environment.SpecialFolderOption.DoNotVerify);
            }
            catch
            {
                homeDirectory = null;
            }

            return new GitEnvironment(
                homeDirectory: homeDirectory,
                xdgConfigHomeDirectory: getVariable("XDG_CONFIG_HOME"),
                programDataDirectory: getVariable("PROGRAMDATA"),
                systemDirectory: FindSystemDirectory(getVariable("PATH"), getVariable("MICROSOFT_SOURCELINK_TEST_ENVIRONMENT_ETC_DIR")));
        }

        // internal for testing
        internal static string? FindSystemDirectory(string? pathList, string? unixEtcDir)
        {
            if (PathUtils.IsUnixLikePlatform)
            {
                return string.IsNullOrEmpty(unixEtcDir) ? "/etc" : unixEtcDir;
            }

            var gitInstallDir = FindWindowsGitInstallation(pathList);
            if (gitInstallDir != null)
            {
                return Path.Combine(gitInstallDir, "etc");
            }

            return null;
        }

        private static string? FindWindowsGitInstallation(string? pathList)
        {
            Debug.Assert(!PathUtils.IsUnixLikePlatform);

            if (NullableString.IsNullOrEmpty(pathList))
            {
                return null;
            }

            var paths = pathList.Split(Path.PathSeparator);

            var gitExe = paths.FirstOrDefault(dir => !string.IsNullOrWhiteSpace(dir) && File.Exists(PathUtils.CombinePaths(dir, "git.exe")));
            if (gitExe != null)
            {
                return Path.GetDirectoryName(gitExe);
            }

            var gitCmd = paths.FirstOrDefault(dir => !string.IsNullOrWhiteSpace(dir) && File.Exists(PathUtils.CombinePaths(dir, "git.cmd")));
            if (gitCmd != null)
            {
                return Path.GetDirectoryName(gitCmd);
            }

            return null;
        }

        internal string GetHomeDirectoryForPathExpansion(string path)
            => HomeDirectory ?? throw new NotSupportedException(
                string.Format(Resources.HomeRelativePathsAreNotAllowed, GitRepositoryConfigurationScopeName, LocalConfigurationScopeName, path));
    }
}
