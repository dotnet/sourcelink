﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal static class TestUtilities
    {
        public static string InspectSourceRoot(ITaskItem sourceRoot)
        {
            var sourceControl = sourceRoot.GetMetadata("SourceControl");
            var revisionId = sourceRoot.GetMetadata("RevisionId");
            var nestedRoot = sourceRoot.GetMetadata("NestedRoot");
            var containingRoot = sourceRoot.GetMetadata("ContainingRoot");
            var scmRepositoryUrl = sourceRoot.GetMetadata("ScmRepositoryUrl");
            var sourceLinkUrl = sourceRoot.GetMetadata("SourceLinkUrl");

            return $"'{sourceRoot.ItemSpec}'" +
              (string.IsNullOrEmpty(sourceControl) ? "" : $" SourceControl='{sourceControl}'") +
              (string.IsNullOrEmpty(revisionId) ? "" : $" RevisionId='{revisionId}'") +
              (string.IsNullOrEmpty(nestedRoot) ? "" : $" NestedRoot='{nestedRoot}'") +
              (string.IsNullOrEmpty(containingRoot) ? "" : $" ContainingRoot='{containingRoot}'") +
              (string.IsNullOrEmpty(scmRepositoryUrl) ? "" : $" ScmRepositoryUrl='{scmRepositoryUrl}'") +
              (string.IsNullOrEmpty(sourceLinkUrl) ? "" : $" SourceLinkUrl='{sourceLinkUrl}'");
        }

        public static string InspectDiagnostic((string Message, object?[] Args) warning)
            => string.Format(warning.Message, warning.Args);

        public static string? GetExceptionMessage(Action action)
        {
            try
            {
                action();
                return null;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }
    }
}
