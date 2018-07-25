// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Diagnostics;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink
{
    /// <summary>
    /// URL parsing utilities for Team Foundation source control providers (VSTS, TFS, TFVC).
    /// </summary>
    internal static class TeamFoundationUrlParser
    {
        public static bool IsVisualStudioHostedServer(string host)
           => host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase) ||
              host.EndsWith(".vsts.me", StringComparison.OrdinalIgnoreCase);

        public static bool TryParseHostedHttp(string host, string relativeUrl, out string repositoryPath, out string repositoryName)
        {
            repositoryPath = repositoryName = null;

            var parts = SplitRelativeUrl(relativeUrl);
            if (parts.Length == 0)
            {
                return false;
            }

            int index = 0;
            string accountPath;

            if (IsVisualStudioHostedServer(host))
            {
                // account is stored in the domain, not in the path:
                accountPath = null;

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
                accountPath = parts[index++];
            }

            var result = TryParsePath(parts, index, "_git", out repositoryPath, out repositoryName);

            if (accountPath != null)
            {
                repositoryPath = UriUtilities.Combine(accountPath, repositoryPath);
            }

            return result;
        }

        public static bool TryParseHostedSsh(Uri uri, out string account, out string repositoryPath, out string repositoryName)
        {
            Debug.Assert(uri != null);

            account = repositoryPath = repositoryName = null;

            // {"DefaultCollection"|""}/{repositoryPath}/"_ssh"/{"_full"|"_optimized"}/{repositoryName}
            string[] parts = SplitRelativeUrl(uri.LocalPath);
            if (parts.Length == 0)
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

        public static bool TryParseOnPremHttp(string relativeUrl, out string repositoryPath, out string repositoryName)
            => TryParsePath(SplitRelativeUrl(relativeUrl), startIndex: 0, "_git", out repositoryPath, out repositoryName);

        public static bool TryParseOnPremSsh(Uri uri, out string repositoryPath, out string repositoryName)
            => TryParsePath(SplitRelativeUrl(uri.LocalPath), startIndex: 0, "_ssh", out repositoryPath, out repositoryName);

        private static string[] SplitRelativeUrl(string relativeUrl)
        {
            // required leading slash:
            if (relativeUrl.Length <= 2 || relativeUrl[0] != '/')
            {
                return Array.Empty<string>();
            }

            // optional trailing slash:
            int end = relativeUrl.Length - 1;
            if (relativeUrl[end] == '/')
            {
                end--;
            }

            var result = relativeUrl.Substring(1, end).Split(new[] { '/' });
            return result.Any(part => part.Length == 0) ? Array.Empty<string>() : result;
        }

        private static bool TryParsePath(string[] parts, int startIndex, string type, out string repositoryPath, out string repositoryName)
        {
            Debug.Assert(type == "_git" || type == "_ssh" || type == null);

            // {repositoryPath}/{type}/{"_full"|"_optimized"|""}/{repositoryName}

            repositoryPath = repositoryName = null;

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

            if (parts[i] == "_full" || parts[i] == "_optimized")
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
                if (parts[i] == "_ssh" || parts[i] == "_git")
                {
                    return false;
                }
            }

            repositoryPath = string.Join("/", parts, startIndex, i - startIndex + 1);
            return true;
        }
    }
}
