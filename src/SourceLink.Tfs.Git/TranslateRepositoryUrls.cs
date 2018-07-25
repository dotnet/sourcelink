// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Tfs.Git
{
    public sealed class TranslateRepositoryUrls : TranslateRepositoryUrlsGitTask
    {
        // Translates
        //   ssh://{account}@{domain}:{port}/{repositoryPath}/_ssh/{"_full"|"_optimized"}/{repositoryName}
        // to
        //   https://{domain}/{repositoryPath}/_git/{repositoryName}
        protected override string TranslateSshUrl(Uri uri)
        {
            if (!TeamFoundationUrlParser.TryParseOnPremSsh(uri, out var repositoryPath, out var repositoryName))
            {
                return null;
            }

            return
                UriUtilities.Combine(
                UriUtilities.Combine("https://" + uri.Host, repositoryPath), "_git/" + repositoryName);
        }
    }
}
