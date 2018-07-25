// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.SourceControl
{
    internal static class UriUtilities
    {
        public static bool TryParseAuthority(string value, out Uri uri)
            => Uri.TryCreate("unknown://" + value, UriKind.Absolute, out uri) && IsAuthorityUri(uri);

        public static int GetExplicitPort(this Uri uri)
            => new Uri("unknown://" + uri.Authority, UriKind.Absolute).Port;

        public static string Combine(string baseUrl, string relativeUrl)
            => string.IsNullOrEmpty(relativeUrl) ? baseUrl : 
                baseUrl.EndsWith("/")
                    ? (relativeUrl.StartsWith("/") ? baseUrl + relativeUrl.Substring(1) : baseUrl + relativeUrl)
                    : (relativeUrl.StartsWith("/") ? baseUrl + relativeUrl : baseUrl + "/" + relativeUrl);

        public static bool IsAuthorityUri(Uri uri)
            => uri.PathAndQuery == "/" && uri.UserInfo == "";
    }
}
