// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Tasks.SourceControl
{
    internal static class UriUtilities
    {
        public static bool TryParseAuthority(string value, [NotNullWhen(true)] out Uri? uri)
            => Uri.TryCreate("unknown://" + value, UriKind.Absolute, out uri) && 
               uri.Scheme == "unknown" && uri.Host != "" && uri.UserInfo == "" && uri.PathAndQuery == "/";

        public static int GetExplicitPort(this Uri uri)
            => new Uri("unknown://" + uri.GetAuthority(), UriKind.Absolute).Port;

        public static string Combine(string baseUrl, string relativeUrl)
            => string.IsNullOrEmpty(relativeUrl) ? baseUrl : 
                baseUrl.EndsWith("/")
                    ? (relativeUrl.StartsWith("/") ? baseUrl + relativeUrl.Substring(1) : baseUrl + relativeUrl)
                    : (relativeUrl.StartsWith("/") ? baseUrl + relativeUrl : baseUrl + "/" + relativeUrl);

        public static bool UrlStartsWith(string url, string prefix)
        {
            if (!url.EndsWith("/", StringComparison.Ordinal))
            {
                url += "/";
            }

            if (!prefix.EndsWith("/", StringComparison.Ordinal))
            {
                prefix += "/";
            }

            return url.StartsWith(prefix, StringComparison.Ordinal);
        }

        public static bool TrySplitRelativeUrl(string relativeUrl, [NotNullWhen(true)] out string[]? parts)
        {
            // required leading slash:
            if (relativeUrl.Length == 0 || relativeUrl == "/")
            {
                parts = Array.Empty<string>();
                return true;
            }

            if (relativeUrl == "//")
            {
                parts = null;
                return false;
            }

            int start = (relativeUrl[0] == '/') ? 1 : 0;

            // optional trailing slash:
            int end = relativeUrl.Length - 1;
            if (relativeUrl[end] == '/')
            {
                end--;
            }

            parts = relativeUrl.Substring(start, end - start + 1).Split(new[] { '/' });
            return !parts.Any(part => part.Length == 0);
        }

        public static string GetScheme(this Uri uri)
            => uri.GetComponents(UriComponents.Scheme, UriFormat.SafeUnescaped);

        public static string GetHost(this Uri uri)
            => uri.GetComponents(UriComponents.Host, UriFormat.SafeUnescaped);

        public static string GetAuthority(this Uri uri)
            => uri.GetComponents(UriComponents.Host | UriComponents.Port, UriFormat.SafeUnescaped);

        public static string GetPath(this Uri uri)
            => uri.GetComponents(UriComponents.Path | UriComponents.KeepDelimiter, UriFormat.SafeUnescaped);

        public static string GetPathAndQuery(this Uri uri)
            => uri.GetComponents(UriComponents.PathAndQuery, UriFormat.SafeUnescaped);
    }
}
