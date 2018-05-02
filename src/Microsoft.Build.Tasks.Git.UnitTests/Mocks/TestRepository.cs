﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestRepository : IRepository
    {
        private readonly IReadOnlyList<Remote> _remotes;
        private readonly IReadOnlyList<Submodule> _submodules;
        private readonly IReadOnlyList<string> _ignoredPaths;
        private readonly IReadOnlyDictionary<string, object> _config;
        private readonly string _headTipCommitShaOpt;
        private readonly string _workingDir;

        public TestRepository(
            string workingDir,
            string commitSha,
            IReadOnlyList<Remote> remotes = null,
            IReadOnlyList<Submodule> submodules = null,
            IReadOnlyList<string> ignoredPaths = null,
            IReadOnlyDictionary<string, object> config = null,
            bool submodulesSupported = true)
        {
            _workingDir = workingDir;
            _headTipCommitShaOpt = commitSha;
            _remotes = remotes ?? new Remote[0];
            _submodules = submodulesSupported ? (submodules ?? new Submodule[0]) : null;
            _ignoredPaths = ignoredPaths ?? new string[0];
            _config = config ?? new Dictionary<string, object>();
        }

        public RepositoryInformation Info
            => new TestRepositoryInformation(_workingDir);

        public Network Network 
            => new TestNetwork(_remotes);

        public Branch Head
            => new TestBranch(_headTipCommitShaOpt);

        public SubmoduleCollection Submodules
            => new TestSubmoduleCollection(_submodules);

        public Ignore Ignore
            => new TestIgnore(_ignoredPaths);

        public Configuration Config
            => new TestConfiguration(_config);

        #region Not Implemented

        public Index Index => throw new NotImplementedException();

        public ReferenceCollection Refs => throw new NotImplementedException();

        public IQueryableCommitLog Commits => throw new NotImplementedException();

        public BranchCollection Branches => throw new NotImplementedException();

        public TagCollection Tags => throw new NotImplementedException();

        public Diff Diff => throw new NotImplementedException();

        public ObjectDatabase ObjectDatabase => throw new NotImplementedException();

        public NoteCollection Notes => throw new NotImplementedException();

        public Rebase Rebase => throw new NotImplementedException();
        
        public StashCollection Stashes => throw new NotImplementedException();

        public BlameHunkCollection Blame(string path, BlameOptions options)
        {
            throw new NotImplementedException();
        }

        public void Checkout(Tree tree, IEnumerable<string> paths, CheckoutOptions opts)
        {
            throw new NotImplementedException();
        }

        public void CheckoutPaths(string committishOrBranchSpec, IEnumerable<string> paths, CheckoutOptions checkoutOptions)
        {
            throw new NotImplementedException();
        }

        public CherryPickResult CherryPick(Commit commit, Signature committer, CherryPickOptions options)
        {
            throw new NotImplementedException();
        }

        public Commit Commit(string message, Signature author, Signature committer, CommitOptions options)
        {
            throw new NotImplementedException();
        }

        public string Describe(Commit commit, DescribeOptions options)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public GitObject Lookup(ObjectId id)
        {
            throw new NotImplementedException();
        }

        public GitObject Lookup(string objectish)
        {
            throw new NotImplementedException();
        }

        public GitObject Lookup(ObjectId id, ObjectType type)
        {
            throw new NotImplementedException();
        }

        public GitObject Lookup(string objectish, ObjectType type)
        {
            throw new NotImplementedException();
        }

        public MergeResult Merge(Commit commit, Signature merger, MergeOptions options)
        {
            throw new NotImplementedException();
        }

        public MergeResult Merge(Branch branch, Signature merger, MergeOptions options)
        {
            throw new NotImplementedException();
        }

        public MergeResult Merge(string committish, Signature merger, MergeOptions options)
        {
            throw new NotImplementedException();
        }

        public MergeResult MergeFetchedRefs(Signature merger, MergeOptions options)
        {
            throw new NotImplementedException();
        }

        public void RemoveUntrackedFiles()
        {
            throw new NotImplementedException();
        }

        public void Reset(ResetMode resetMode, Commit commit)
        {
            throw new NotImplementedException();
        }

        public void Reset(ResetMode resetMode, Commit commit, CheckoutOptions options)
        {
            throw new NotImplementedException();
        }

        public FileStatus RetrieveStatus(string filePath)
        {
            throw new NotImplementedException();
        }

        public RepositoryStatus RetrieveStatus(StatusOptions options)
        {
            throw new NotImplementedException();
        }

        public RevertResult Revert(Commit commit, Signature reverter, RevertOptions options)
        {
            throw new NotImplementedException();
        }

        public void RevParse(string revision, out Reference reference, out GitObject obj)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
