﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Vsts.Git
{
    public sealed class TranslateRepositoryUrls : TranslateRepositoryUrlsGitTask
    {
        // Translates
        //   ssh://{account}@{ssh-subdomain}.{domain}:{port}/{repositoryPath}/_ssh/{"_full"|"_optimized"}/{repositoryName}
        // to
        //   https://{http-domain}/{account}/{repositoryPath}/_git/{repositoryName}
        //
        // Dommain mapping:
        //   ssh://vs-ssh.*.com -> https://{account}.*.com 
        //   ssh://ssh.*.com -> https://*.com/{account}
        protected override string TranslateSshUrl(Uri uri)
        {
            var isVisualStudioHost = TeamFoundationUrlParser.IsVisualStudioHostedServer(uri.Host);
            var prefix = isVisualStudioHost ? "vs-ssh." : "ssh.";
            if (!uri.Host.StartsWith(prefix))
            {
                return null;
            }

            if (!TeamFoundationUrlParser.TryParseHostedSsh(uri, out var account, out var repositoryPath, out var repositoryName))
            {
                return null;
            }

            var result = uri.Host.Substring(prefix.Length);
            if (isVisualStudioHost)
            {
                result = account + "." + result;
            }
            else
            {
                result = result + "/" + account;
            }

            return
                UriUtilities.Combine(
                UriUtilities.Combine("https://" + result, repositoryPath), "_git/" + repositoryName);
        }
    }
}
