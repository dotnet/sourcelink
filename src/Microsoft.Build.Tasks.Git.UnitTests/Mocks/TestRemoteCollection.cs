// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestRemoteCollection : RemoteCollection
    {
        private readonly IReadOnlyList<Remote> _remotes;

        public TestRemoteCollection(IReadOnlyList<Remote> remotes)
        {
            _remotes = remotes;
        }

        public override Remote this[string name] 
            => _remotes.FirstOrDefault(r => r.Name == name);

        public override IEnumerator<Remote> GetEnumerator()
            => _remotes.GetEnumerator();
    }
}
