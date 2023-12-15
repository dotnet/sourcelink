// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.SourceControl
{
    public class TranslateRepositoryUrlsGitTask : Task
    {
        private const string SourceControlName = "git";

        public string? RepositoryUrl { get; set; }
        public ITaskItem[]? SourceRoots { get; set; }

        public ITaskItem[]? Hosts { get; set; }
        public bool IsSingleProvider { get; set; }

        [Output]
        public string? TranslatedRepositoryUrl { get; set; }

        [Output]
        public ITaskItem[]? TranslatedSourceRoots { get; set; }

        protected virtual string? TranslateSshUrl(Uri uri)
            => "https://" + uri.GetHost() + uri.GetPathAndQuery();

        protected virtual string? TranslateGitUrl(Uri uri)
            => "https://" + uri.GetHost() + uri.GetPathAndQuery();

        protected virtual string? TranslateHttpUrl(Uri uri)
            => uri.GetScheme() + "://" + uri.GetAuthority() + uri.GetPathAndQuery();

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            // Assign translated roots even when the task fails (or no Hosts were specified) to avoid cascading errors.
            TranslatedSourceRoots = SourceRoots;

            var hostUris = GetHostUris().ToArray();
            if (hostUris.Length == 0)
            {
                Log.LogMessage(CommonResources.NoWellFormedHostUrisSpecified, "'" + string.Join("','", (Hosts ?? Array.Empty<ITaskItem>()).Select(h => h.ItemSpec)) + "'");
                return;
            }

            static bool isMatchingHostUri(Uri hostUri, Uri uri)
                => uri.GetHost().Equals(hostUri.GetHost(), StringComparison.OrdinalIgnoreCase) ||
                   uri.GetHost().EndsWith("." + hostUri.GetHost(), StringComparison.OrdinalIgnoreCase);

            // only need to translate valid ssh URLs that match one of our hosts:
            string? translate(string? url)
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                    hostUris.Any(h => isMatchingHostUri(h, uri)))
                {
                    return (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) ? TranslateHttpUrl(uri) :
                            uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? TranslateHttpUrl(uri) :
                            uri.Scheme.Equals("ssh", StringComparison.OrdinalIgnoreCase) ? TranslateSshUrl(uri) :
                            uri.Scheme.Equals("git", StringComparison.OrdinalIgnoreCase) ? TranslateGitUrl(uri) : null) ?? url;
                }

                return url;
            }

            try
            {
                TranslatedRepositoryUrl = translate(RepositoryUrl);
            }
            catch (NotSupportedException e)
            {
                Log.LogError(e.Message);
                return;
            }

            if (TranslatedSourceRoots != null)
            {
                foreach (var sourceRoot in TranslatedSourceRoots)
                {
                    if (!string.Equals(sourceRoot.GetMetadata(Names.SourceRoot.SourceControl), SourceControlName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string? translatedUrl;
                    try
                    {
                        translatedUrl = translate(sourceRoot.GetMetadata(Names.SourceRoot.ScmRepositoryUrl));
                    }
                    catch (NotSupportedException e)
                    {
                        Log.LogError(e.Message);
                        continue;
                    }

                    // Item metadata are stored msbuild-escaped. GetMetadata unescapes, SetMetadata
                    // stores the value as specified. When initializing the URL metadata from git
                    // information we msbuild-escaped the URL to preserve any URL escapes in it.
                    // Here, GetMetadata unescapes the msbuild escapes, then we translate the URL
                    // and finally msbuild-escape the resulting URL to preserve any URL escapes.
                    sourceRoot.SetMetadata(Names.SourceRoot.ScmRepositoryUrl, Evaluation.ProjectCollection.Escape(translatedUrl));
                }
            }
        }

        private IEnumerable<Uri> GetHostUris()
        {
            if (Hosts != null)
            {
                foreach (var item in Hosts)
                {
                    if (UriUtilities.TryParseAuthority(item.ItemSpec, out var hostUri))
                    {
                        yield return hostUri;
                    }
                    else
                    {
                        Log.LogWarning(CommonResources.IgnoringInvalidHostName, item.ItemSpec);
                    }
                }
            }

            // Add implicit host last, so that matching prefers explicitly listed hosts over the implicit one.
            if (IsSingleProvider && Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var repositoryUri))
            {
                yield return repositoryUri;
            }
        }
    }
}
