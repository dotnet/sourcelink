// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.Common
{
    public sealed class GetImplicitRepositoryHost : Task
    {
        public string ProviderTargets { get; set; }

        public string RepositoryUrl { get; set; }

        [Output]
        public string ImplicitHost { get; set; }

        public override bool Execute()
        {
            ExecuteImpl();
            return !Log.HasLoggedErrors;
        }

        private void ExecuteImpl()
        {
            var targets = (ProviderTargets ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (targets.Length == 0)
            {
                Log.LogError(Resources.NoSourceLinkPackagesReferenced);
                return;
            }

            if (targets.Length > 1)
            {
                // multiple providers, do not define an implicit host
                return;
            }

            if (!Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out var uri))
            {
                // TODO: report error?
                // unable to define an implicit host
                return;
            }

            ImplicitHost = uri.Authority;
        }
    }
}
