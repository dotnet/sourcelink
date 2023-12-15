// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Linq;
using System.Diagnostics.CodeAnalysis;
using System.IO;

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

        /// <summary>
        /// Converts an absolute local file path or an absolute URL string to <see cref="Uri"/>.
        /// </summary>
        /// <exception cref="UriFormatException">
        /// The <paramref name="absolutePath"/> can't be represented as <see cref="Uri"/>.
        /// For example, UNC paths with invalid characters in server name.
        /// </exception>
        public static Uri CreateAbsoluteUri(string absolutePath)
        {
            var uriString = IsAscii(absolutePath) ? absolutePath : GetAbsoluteUriString(absolutePath);
            try
            {
#pragma warning disable RS0030 // Do not use banned APIs
                return new(uriString, UriKind.Absolute);
#pragma warning restore

            }
            catch (UriFormatException e)
            {
                // The standard URI format exception does not include the failing path, however
                // in pretty much all cases we need to know the URI string (and original string) in order to fix the issue.
                throw new UriFormatException($"Failed create URI from '{uriString}'; original string: '{absolutePath}'", e);
            }
        }

        // Implements workaround for https://github.com/dotnet/runtime/issues/89538:
        internal static string GetAbsoluteUriString(string absolutePath)
        {
            if (!PathUtilities.IsAbsolute(absolutePath))
            {
                return absolutePath;
            }

            var parts = absolutePath.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]);

            if (PathUtilities.IsUnixLikePlatform)
            {
                // Unix path: first part is empty, all parts should be escaped
                return "file://" + string.Join("/", parts.Select(EscapeUriPart));
            }

            if (parts is ["", "", var serverName, ..])
            {
                // UNC path: first non-empty part is server name and shouldn't be escaped
                return "file://" + serverName + "/" + string.Join("/", parts.Skip(3).Select(EscapeUriPart));
            }

            // Drive-rooted path: first part is "C:" and shouldn't be escaped
            return "file:///" + parts[0] + "/" + string.Join("/", parts.Skip(1).Select(EscapeUriPart));

#pragma warning disable SYSLIB0013 // Type or member is obsolete
            static string EscapeUriPart(string stringToEscape)
                => Uri.EscapeUriString(stringToEscape).Replace("#", "%23");
#pragma warning restore
        }

        private static bool IsAscii(char c)
            => (uint)c <= '\x007f';

        private static bool IsAscii(string filePath)
        {
            for (var i = 0; i < filePath.Length; i++)
            {
                if (!IsAscii(filePath[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
