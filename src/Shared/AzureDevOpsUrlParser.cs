// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink
{
    /// <summary>
    /// URL parsing utilities for Azure DevOps source control providers (Services and Server).
    /// See https://github.com/dotnet/sourcelink/blob/master/docs/Implementation/AzureDevOpsUrlParser.md
    /// </summary>
    internal static class AzureDevOpsUrlParser
    {
        public static bool IsVisualStudioHostedServer(string host)
           => host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
              host.EndsWith(".vsts.me", StringComparison.OrdinalIgnoreCase);

        public static bool TryParseHostedHttp(string host, string relativeUrl, [NotNullWhen(true)]out string? projectPath, [NotNullWhen(true)]out string? repositoryName)
        {
            projectPath = repositoryName = null;

            if (!UriUtilities.TrySplitRelativeUrl(relativeUrl, out var parts) || parts.Length == 0)
            {
                return false;
            }

            int index = 0;
            string? account;
            bool isVisualStudioHost = IsVisualStudioHostedServer(host);

            if (isVisualStudioHost)
            {
                // account is stored in the domain, not in the path:
                account = null;

                // Trim optional "DefaultCollection" from path:
                if (StringComparer.OrdinalIgnoreCase.Equals(parts[index], "DefaultCollection"))
                {
                    index++;
                }
            }
            else
            {
                // Check this is not an 'enterprise' discovery page URL
                if (StringComparer.OrdinalIgnoreCase.Equals(parts[0], "e"))
                {
                    return false;
                }

                // Account is first part of path:
                account = parts[index++];
            }

            if (index == parts.Length)
            {
                return false;
            }

            if (!TryParsePath(parts, index, "_git", out var projectName, out var teamName, out repositoryName))
            {
                return false;
            }

            projectPath = projectName ?? repositoryName;
           
            if (!isVisualStudioHost)
            {
                if (teamName != null)
                {
                    return false;
                }

                projectPath = account + "/" + projectPath;
            }

            return true;
        }

        public static bool TryParseOnPremHttp(string relativeUrl, string virtualDirectory, [NotNullWhen(true)]out string? projectPath, [NotNullWhen(true)]out string? repositoryName)
        {
            projectPath = repositoryName = null;

            if (!UriUtilities.TrySplitRelativeUrl(relativeUrl, out var parts) || parts.Length == 0 ||
                !UriUtilities.TrySplitRelativeUrl(virtualDirectory, out var virtualDirectoryParts))
            {
                return false;
            }

            // skip virtual directory:
            if (!parts.Take(virtualDirectoryParts.Length).SequenceEqual(virtualDirectoryParts, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            // collection:
            int i = virtualDirectoryParts.Length;
            var collection = parts[i++];

            if (!TryParsePath(parts, i, "_git", out var projectName, out _, out repositoryName))
            {
                return false;
            }

            projectPath = string.Join("/", parts, 0, virtualDirectoryParts.Length) + "/" + collection + "/" + (projectName ?? repositoryName);
            return true;
        }

        public static bool TryParseHostedSsh(Uri uri, [NotNullWhen(true)]out string? account, [NotNullWhen(true)]out string? repositoryPath, [NotNullWhen(true)]out string? repositoryName)
        {
            NullableDebug.Assert(uri != null);

            account = repositoryPath = repositoryName = null;

            // {"DefaultCollection"|""}/{repositoryPath}/"_ssh"/{"_full"|"_optimized"}/{repositoryName}
            if (!UriUtilities.TrySplitRelativeUrl(uri.GetPath(), out var parts) || parts.Length == 0)
            {
                return false;
            }

            // Check for v3 url format
            if (parts[0] == "v3" &&
                parts.Length >= 3 &&
                TryParsePath(parts, 2, type: null, out repositoryPath, out repositoryName) &&
                repositoryPath != "")
            {
                // ssh://{user}@{domain}:{port}/v3/{account}/{repositoryPath}/{'_full'|'_optimized'|''}/{repositoryName}
                account = parts[1];
            }
            else
            {
                // ssh v1/v2 url formats
                // ssh://{account}@vs-ssh.visualstudio.com/

                account = uri.UserInfo;

                int index = 0;
                if (StringComparer.OrdinalIgnoreCase.Equals(parts[0], "DefaultCollection"))
                {
                    index++;
                }

                if (!TryParsePath(parts, index, "_ssh", out repositoryPath, out repositoryName))
                {
                    // Failed to parse path
                    return false;
                }
            }

            if (account.Length == 0)
            {
                return false;
            }

            return true;
        }

        public static bool TryParseOnPremSsh(Uri uri, [NotNullWhen(true)]out string? repositoryPath, [NotNullWhen(true)]out string? repositoryName)
        {
            repositoryPath = repositoryName = null;

            if (!UriUtilities.TrySplitRelativeUrl(uri.GetPath(), out var parts))
            {
                return false;
            }

            if (!TryParseRepositoryName(parts, out int teamNameIndex, "_ssh", out repositoryName))
            {
                return false;
            }

            repositoryPath = string.Join("/", parts, 0, teamNameIndex + 1);
            return true;
        }
        
        private static bool TryParsePath(string[] parts, int startIndex, string? type, [NotNullWhen(true)]out string? repositoryPath, [NotNullWhen(true)]out string? repositoryName)
        {
            if (TryParsePath(parts, startIndex, type, out var projectName, out var teamName, out repositoryName))
            {
                repositoryPath = (projectName != null && teamName != null) ? projectName + "/" + teamName : (projectName ?? teamName ?? "");
                return true;
            }

            repositoryPath = null;
            return false;
        }

        private static bool TryParsePath(string[] parts, int projectPartIndex, string? type, out string? projectName, out string? teamName, [NotNullWhen(true)]out string? repositoryName)
        {
            // {projectName?}/{teamName?}/{type}/{"_full"|"_optimized"|""}/{repositoryName}

            projectName = teamName = null;

            if (!TryParseRepositoryName(parts, out int teamNameIndex, type, out repositoryName))
            {
                return false;
            }

            switch (teamNameIndex - projectPartIndex + 1)
            {
                case 0:
                    return true;

                case 1:
                    projectName = parts[projectPartIndex];
                    return true;

                case 2:
                    projectName = parts[projectPartIndex];
                    teamName = parts[projectPartIndex + 1];
                    return true;

                default:
                    return false;
            }
        }

        private static bool TryParseRepositoryName(string[] parts, out int teamNameIndex, string? type, [NotNullWhen(true)]out string? repositoryName)
        {
            Debug.Assert(type is "_git" or "_ssh" or null);

            // {type}/{"_full"|"_optimized"|""}/{repositoryName}

            repositoryName = null;
            teamNameIndex = -1;

            int i = parts.Length - 1;

            if (i < 0)
            {
                return false;
            }

            repositoryName = parts[i--];

            if (i < 0)
            {
                return false;
            }

            if (parts[i] is "_full" or "_optimized")
            {
                i--;

                if (i < 0)
                {
                    return false;
                }
            }

            // _git or _ssh
            if (type != null)
            {
                if (parts[i--] != type)
                {
                    return false;
                }
            }
            else
            {
                if (parts[i] is "_ssh" or "_git")
                {
                    return false;
                }
            }

            teamNameIndex = i;
            return true;
        }
    }
}
