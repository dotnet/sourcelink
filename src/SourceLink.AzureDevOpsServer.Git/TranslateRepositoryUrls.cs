// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.AzureDevOpsServer.Git
{
    public sealed class TranslateRepositoryUrls : TranslateRepositoryUrlsGitTask
    {
        // Translates
        //   ssh://{account}@{domain}:{port}/{repositoryPath}/_ssh/{"_full"|"_optimized"}/{repositoryName}
        // to
        //   https://{domain}/{repositoryPath}/_git/{repositoryName}
        protected override string? TranslateSshUrl(Uri uri)
        {
            if (!AzureDevOpsUrlParser.TryParseOnPremSsh(uri, out var repositoryPath, out var repositoryName))
            {
                return null;
            }

            return
                UriUtilities.Combine(
                UriUtilities.Combine("https://" + uri.Host, repositoryPath), "_git/" + repositoryName);
        }
    }
}
