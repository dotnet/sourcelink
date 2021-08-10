// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using Microsoft.Build.Tasks.SourceControl;
using System;

namespace Microsoft.SourceLink.GitWeb
{
    /// <summary>
    /// This task normally translates all protocols to HTTPS git repo URLs for consistency. Not all
    /// GitWebs support HTTPS URLs for clones. So instead we keep the SSH URL. The
    /// <see cref="GetSourceLinkUrl"/> Task converts the URLs to HTTP content URLs which GitWeb does
    /// support. The output of this Task is independent of <see cref="GetSourceLinkUrl"/>.
    /// </summary>
    public sealed class TranslateRepositoryUrls : TranslateRepositoryUrlsGitTask
    {
        /// <summary>
        /// Keep the SSH URL. It is the only protocol currently known to work with GitWeb.
        /// </summary>
        /// <param name="uri"></param>
        /// <returns><paramref name="uri"/> as a string</returns>
        protected override string TranslateSshUrl(Uri uri)
            => uri.ToString();

        protected override string TranslateGitUrl(Uri uri)
            => throw new NotSupportedException(string.Format(Resources.RepositoryUrlIsNotSupportedByProvider, "GIT"));

        protected override string TranslateHttpUrl(Uri uri)
            => throw new NotSupportedException(string.Format(Resources.RepositoryUrlIsNotSupportedByProvider, "HTTP"));
    }
}
