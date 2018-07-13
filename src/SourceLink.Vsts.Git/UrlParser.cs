// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.SourceLink.Vsts.Git
{
    internal static class UrlParser
    {
        public static bool TryParseRelativeRepositoryUrl(string relativeUrl, string protocolName, out string collectionName, out string projectName, out string repositoryName)
        {
            // Relative URL pattern:
            // /[{collection}/]?{project}/{protocolName}/{repository-name}

            projectName = null;
            repositoryName = null;
            collectionName = null;

            if (relativeUrl.Length <= 1 || relativeUrl[0] != '/')
            {
                return false;
            }

            // trim leading and optional trailing slash:
            relativeUrl = relativeUrl.Substring(1, (relativeUrl[relativeUrl.Length - 1] == '/') ? relativeUrl.Length - 2 : relativeUrl.Length - 1);

            var parts = relativeUrl.Split('/');
            if (parts.Length < 3 || parts.Length > 4)
            {
                return false;
            }

            if (parts.Length == 4)
            {
                collectionName = parts[0];
                if (collectionName.Length == 0)
                {
                    return false;
                }
            }

            if (!parts[parts.Length - 2].Equals(protocolName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            repositoryName = parts[parts.Length - 1];
            projectName = parts[parts.Length - 3];

            if (repositoryName.Length == 0 || projectName.Length == 0)
            {
                return false;
            }

            return true;
        }
    }
}
