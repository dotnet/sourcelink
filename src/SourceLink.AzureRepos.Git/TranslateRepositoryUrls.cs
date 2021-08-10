// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.AzureRepos.Git
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
        protected override string? TranslateSshUrl(Uri uri)
        {
            var host = uri.GetHost();
            var isVisualStudioHost = AzureDevOpsUrlParser.IsVisualStudioHostedServer(host);
            var prefix = isVisualStudioHost ? "vs-ssh." : "ssh.";
            if (!host.StartsWith(prefix))
            {
                return null;
            }

            if (!AzureDevOpsUrlParser.TryParseHostedSsh(uri, out var account, out var repositoryPath, out var repositoryName))
            {
                return null;
            }

            var result = host.Substring(prefix.Length);
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
