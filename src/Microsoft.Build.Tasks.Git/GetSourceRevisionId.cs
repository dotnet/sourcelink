// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git
{
    public sealed class GetSourceRevisionId : RepositoryTask
    {
        [Output]
        public string RevisionId { get; internal set; }

        public override bool Execute() => TaskImplementation.GetSourceRevisionId(this);
    }
}
