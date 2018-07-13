// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.SourceControl
{
    public abstract class TranslateRepositoryUrlsGitTask : Task
    {
        private const string SourceControlName = "git";

        public string RepositoryUrl { get; set; }
        public ITaskItem[] SourceRoots { get; set; }

        public ITaskItem[] Hosts { get; set; }
        public bool IsSingleProvider { get; set; }

        [Output]
        public string TranslatedRepositoryUrl { get; set; }

        [Output]
        public ITaskItem[] TranslatedSourceRoots { get; set; }

        protected virtual string TranslateSshUrl(Uri uri)
            => "https://" + uri.Host + uri.PathAndQuery;

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            var hosts = GetHosts().ToArray();
            if (hosts.Length == 0)
            {
                return;
            }

            bool isMatchingHostUri(Uri uri)
            {
                foreach (var host in hosts)
                {
                    if (uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
                        uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            }

            // only need to translate valid ssh URLs that match one of our hosts:
            string translate(string url)
                => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "ssh" && isMatchingHostUri(uri) ? (TranslateSshUrl(uri) ?? url) : url;

            TranslatedRepositoryUrl = translate(RepositoryUrl);
            TranslatedSourceRoots = SourceRoots;

            if (TranslatedSourceRoots != null)
            {
                foreach (var sourceRoot in TranslatedSourceRoots)
                {
                    if (!string.Equals(sourceRoot.GetMetadata(Names.SourceRoot.SourceControl), SourceControlName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    sourceRoot.SetMetadata(Names.SourceRoot.ScmRepositoryUrl, translate(sourceRoot.GetMetadata(Names.SourceRoot.ScmRepositoryUrl)));
                }
            }
        }

        private IEnumerable<string> GetHosts()
        {
            if (Hosts != null)
            {
                foreach (var item in Hosts)
                {
                    if (UriUtilities.TryParseAuthority(item.ItemSpec, out var authorityUri))
                    {
                        yield return authorityUri.Host;
                    }
                }
            }

            // Add implicit host last, so that matching prefers explicitly listed hosts over the implicit one.
            if (IsSingleProvider && Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri))
            {
                yield return uri.Host;
            }
        }
    }
}
