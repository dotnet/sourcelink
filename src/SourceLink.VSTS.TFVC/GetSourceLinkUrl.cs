﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.SourceLink.VSTS.TFVC
{
    public sealed class GetSourceLinkUrl : Task
    {
        [Required]
        public ITaskItem SourceRoot { get; set; }

        [Output]
        public string SourceLinkUrl { get; set; }

        public override bool Execute()
        {
            if (!string.IsNullOrEmpty(SourceRoot.GetMetadata("SourceLinkUrl")) ||
                !string.Equals(SourceRoot.GetMetadata("SourceControl"), "tfvc", StringComparison.OrdinalIgnoreCase))
            {
                SourceLinkUrl = "N/A";
                return true;
            }

            var collectionUrl = SourceRoot.GetMetadata("CollectionUrl");
            if (!Uri.TryCreate(collectionUrl, UriKind.Absolute, out var collectionUri))
            {
                Log.LogError($"SourceRoot.CollectionUrl of '{SourceRoot.ItemSpec}' is invalid: '{collectionUrl}'");
                return false;
            }

            // 'D' format: "effb7e66-f922-4dc9-a4dc-9bd5d3b01582"
            var projectIdStr = SourceRoot.GetMetadata("ProjectId");
            if (!Guid.TryParseExact(projectIdStr, "D", out var projectId))
            {
                Log.LogError($"SourceRoot.ProjectId of '{SourceRoot.ItemSpec}' is invalid: '{projectIdStr}'");
                return false;
            }

            string revisionIdStr = SourceRoot.GetMetadata("RevisionId");
            if (revisionIdStr == null || !uint.TryParse(revisionIdStr, out var revisionId))
            {
                Log.LogError($"SourceRoot.RevisionId of '{SourceRoot.ItemSpec}' is not a valid changeset number: '{revisionIdStr}'");
                return false;
            }

            string serverPath = SourceRoot.GetMetadata("ServerPath");
            if (serverPath == null || !serverPath.StartsWith("$", StringComparison.Ordinal))
            {
                Log.LogError($"SourceRoot.ServerPath of '{SourceRoot.ItemSpec}' is not a valid server path: '{revisionIdStr}'");
                return false;
            }

            var escapedServerPath = string.Join("/", serverPath.Split('/').Select(Uri.EscapeDataString));

            SourceLinkUrl = new Uri(collectionUri, projectId.ToString("D")).ToString() + 
                "/_versionControl?version=" + revisionId + "&path="+ escapedServerPath + "/*";

            return true;
        }
    }
}
