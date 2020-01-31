// Copyright (c) Microsoft. All Rights Reserved. Licensed under the Apache License, Version 2.0. See
// License.txt in the project root for license information.

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
