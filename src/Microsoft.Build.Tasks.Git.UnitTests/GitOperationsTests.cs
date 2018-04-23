// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks.SourceControl.UnitTests;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitOperationsTests
    {
        private static readonly char s = Path.DirectorySeparatorChar;
        private static readonly string s_root = (s == '/') ? "/usr/src" : @"C:\src";

        private static string InspectSourceRoot(ITaskItem sourceRoot)
        {
            var sourceControl = sourceRoot.GetMetadata("SourceControl");
            var revisionId = sourceRoot.GetMetadata("RevisionId");
            var nestedRoot = sourceRoot.GetMetadata("NestedRoot");
            var containingRoot = sourceRoot.GetMetadata("ContainingRoot");
            var sourceLinkUrl = sourceRoot.GetMetadata("SourceLinkUrl");

            return $"'{sourceRoot.ItemSpec}'" +
              (string.IsNullOrEmpty(sourceControl) ? "" : $" SourceControl='{sourceControl}'") +
              (string.IsNullOrEmpty(revisionId) ? "" : $" RevisionId='{revisionId}'") +
              (string.IsNullOrEmpty(nestedRoot) ? "" : $" NestedRoot='{nestedRoot}'") +
              (string.IsNullOrEmpty(containingRoot) ? "" : $" ContainingRoot='{containingRoot}'") +
              (string.IsNullOrEmpty(sourceLinkUrl) ? "" : $" SourceLinkUrl='{sourceLinkUrl}'");
        }

        private static string InspectDiagnostic((string resourceName, string[] args) warning)
            => (warning.args.Length == 0) ? warning.resourceName : warning.resourceName + ": " + string.Join(",", warning.args);

        [Fact]
        public void GetRevisionId_RepoWithoutCommits()
        {
            var repo = new TestRepository(workingDir: "", commitSha: null);
            Assert.Null(GitOperations.GetRevisionId(repo));
        }

        [Fact]
        public void GetRevisionId_RepoWithCommit()
        {
            var repo = new TestRepository(workingDir: "", commitSha: "8398cdcd9043724b9bef1efda8a703dfaa336c0f");
            Assert.Equal("8398cdcd9043724b9bef1efda8a703dfaa336c0f", GitOperations.GetRevisionId(repo));
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommits()
        {
            var repo = new TestRepository(workingDir: s_root, commitSha: null);

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            Assert.Empty(items);
            AssertEx.Equal(new[] { "RepositoryWithoutCommit_SourceLink" }, warnings.Select(InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RepoWithoutCommitsWithSubmodules()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: null,
                submodules: new[] 
                {
                    new TestSubmodule("1", "sub/1", "http://1.com", "1111111111111111111111111111111111111111"),
                    new TestSubmodule("1", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")
                });

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}sub{s}1{s}' SourceControl='git' RevisionId='1111111111111111111111111111111111111111' NestedRoot='sub/1/' ContainingRoot='{s_root}{s}'",
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] { "RepositoryWithoutCommit_SourceLink" }, warnings.Select(InspectDiagnostic));
        }

        [Fact]
        public void GetSourceRoots_RepoWithCommitsWithSubmodules()
        {
            var repo = new TestRepository(
                workingDir: s_root,
                commitSha: "0000000000000000000000000000000000000000",
                submodules: new[]
                {
                    new TestSubmodule("1", "sub/1", "http://1.com", workDirCommitSha: null),
                    new TestSubmodule("1", "sub/2", "http://2.com", "2222222222222222222222222222222222222222")
                });

            var warnings = new List<(string message, string[] args)>();
            var items = GitOperations.GetSourceRoots(repo, (message, args) => warnings.Add((message, args)));

            AssertEx.Equal(new[]
            {
                $@"'{s_root}{s}' SourceControl='git' RevisionId='0000000000000000000000000000000000000000'",
                $@"'{s_root}{s}sub{s}2{s}' SourceControl='git' RevisionId='2222222222222222222222222222222222222222' NestedRoot='sub/2/' ContainingRoot='{s_root}{s}'",
            }, items.Select(InspectSourceRoot));

            AssertEx.Equal(new[] { "SubmoduleWithoutCommit_SourceLink: 1" }, warnings.Select(InspectDiagnostic));
        }
    }
}
