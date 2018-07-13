// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace Microsoft.Build.Tasks.SourceControl
{
    internal static class Names
    {
        internal static class SourceRoot
        {
            public const string Name = nameof(SourceRoot);

            public const string SourceControl = nameof(SourceControl);
            public const string RepositoryUrl = nameof(RepositoryUrl);
            public const string ScmRepositoryUrl = nameof(ScmRepositoryUrl);
            public const string RevisionId = nameof(RevisionId);
            public const string ContainingRoot = nameof(ContainingRoot);
            public const string NestedRoot = nameof(NestedRoot);
            public const string MappedPath = nameof(MappedPath);
            public const string SourceLinkUrl = nameof(SourceLinkUrl);

            public const string MappedPathFullName = nameof(SourceRoot) + "." + nameof(MappedPath);
            public const string SourceLinkUrlFullName = nameof(SourceRoot) + "." + nameof(SourceLinkUrl);
            public const string RepositoryUrlFullName = nameof(SourceRoot) + "." + nameof(RepositoryUrl);
            public const string ScmRepositoryUrlFullName = nameof(SourceRoot) + "." + nameof(ScmRepositoryUrl);
            public const string RevisionIdFullName = nameof(SourceRoot) + "." + nameof(RevisionId);
        }
    }
}
