// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.Common
{
    public sealed class SourceLinkHasSingleProvider : Task
    {
        public string ProviderTargets { get; set; }

        [Output]
        public bool HasSingleProvider { get; set; }

        public override bool Execute()
        {
            HasSingleProvider = (ProviderTargets ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Length == 1;
            return true;
        }
    }
}
