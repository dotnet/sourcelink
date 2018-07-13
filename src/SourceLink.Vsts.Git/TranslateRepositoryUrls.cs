// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Vsts.Git
{
    public sealed class TranslateRepositoryUrls : TranslateRepositoryUrlsGitTask
    {
        // Translates
        //   ssh://{account}@vs-ssh.{domain}:{port}/{collection}/{project}/_ssh/{repo}
        // to
        //   https://{account}.{domain}/{collection}/{project}/_git/{repo}
        protected override string TranslateSshUrl(Uri uri)
        {
            const string hostPrefix = "vs-ssh.";

            if (!uri.Host.StartsWith(hostPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!UrlParser.TryParseRelativeRepositoryUrl(uri.LocalPath, "_ssh", out var collectionName, out var projectName, out var repositoryName))
            {
                return null;
            }

            return $"https://{uri.UserInfo}.{uri.Host.Substring(hostPrefix.Length)}{(collectionName != null ? "/" + collectionName : "")}/{projectName}/_git/{repositoryName}";
        }
    }
}
