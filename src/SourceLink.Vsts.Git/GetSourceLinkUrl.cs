// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Tasks.SourceControl;

namespace Microsoft.SourceLink.Vsts.Git
{
    public sealed class GetSourceLinkUrl : GetSourceLinkUrlGitTask
    {
        private const string UrlMapEnvironmentVariableName = "BUILD_REPOSITORY_URL_MAP";

        protected override string HostsItemGroupName => "SourceLinkVstsGitHost";
        protected override string ProviderDisplayName => "Vsts.Git";

        protected override Uri GetDefaultContentUriFromHostUri(Uri hostUri, Uri gitUri)
            => TeamFoundationUrlParser.IsVisualStudioHostedServer(gitUri.Host) ?
                new Uri($"{hostUri.Scheme}://{gitUri.Host.Substring(0, gitUri.Host.IndexOf('.'))}.{hostUri.Authority}{hostUri.LocalPath}", UriKind.Absolute) :
                hostUri;

        protected override Uri GetDefaultContentUriFromRepositoryUri(Uri repositoryUri)
           => repositoryUri;

        protected override string BuildSourceLinkUrl(Uri contentUri, string host, string relativeUrl, string revisionId)
        {
            if (!TeamFoundationUrlParser.TryParseHostedHttp(host, relativeUrl, out var repositoryPath, out var repositoryName))
            {
                // TODO: Log.LogError(CommonResources.ValueOfWithIdentityIsInvalid, Names.SourceRoot.RepositoryUrlFullName, SourceRoot.ItemSpec, repoUrl);
                return null;
            }

            return
                UriUtilities.Combine(
                UriUtilities.Combine(contentUri.ToString(), repositoryPath), $"_apis/git/repositories/{repositoryName}/items") +
                $"?api-version=1.0&versionType=commit&version={revisionId}&path=/*";
        }

        // TODO: confirm design and test https://github.com/dotnet/sourcelink/issues/2
        private Dictionary<Uri, Uri> TryGetEnvironmentUriMap()
        {
            var urlSeparators = new[] { Path.PathSeparator };
            Dictionary<Uri, Uri> map = null;

            bool parse(string urls)
            {
                var items = urls.Split(urlSeparators, StringSplitOptions.None);
                if (items.Length % 2 != 0)
                {
                    return false;
                }

                for (int i = 0; i < items.Length; i += 2)
                {
                    string originalUrl = items[i];
                    string mappedUrl = items[i + 1];

                    if (!Uri.TryCreate(originalUrl, UriKind.Absolute, out var originalUri) || originalUri.Query != "")
                    {
                        return false;
                    }

                    if (!Uri.TryCreate(mappedUrl, UriKind.Absolute, out var mappedUri) || mappedUri.Query != "")
                    {
                        return false;
                    }

                    if (map == null)
                    {
                        map = new Dictionary<Uri, Uri>();
                    }

                    map[originalUri] = mappedUri;
                }

                return true;
            }

            IEnumerable<KeyValuePair<string, string>> enumerateVariables()
            {
                int i = 0;
                while (true)
                {
                    var name = UrlMapEnvironmentVariableName + (i == 0 ? "" : i.ToString());
                    var value = Environment.GetEnvironmentVariable(name);
                    if (string.IsNullOrEmpty(value))
                    {
                        yield break;
                    }

                    yield return new KeyValuePair<string, string>(name, value);
                    i++;
                }
            }

            foreach (var variable in enumerateVariables())
            {
                if (!parse(variable.Value))
                {
                    Log.LogError(Resources.EnvironmentVariableIsNotAlistOfUrlPairs, variable.Key, variable.Value);
                    return null;
                }
            }

            return map;
        }
    }
}
