// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.Common
{
    public sealed class SourceLinkHasSingleProvider : Task
    {
        public string? ProviderTargets { get; set; }

        [Output]
        public bool HasSingleProvider { get; set; }

        public override bool Execute()
        {
            HasSingleProvider = (ProviderTargets ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Length == 1;
            return true;
        }
    }
}
