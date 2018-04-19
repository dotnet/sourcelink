// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestNetwork : Network
    {
        private readonly IReadOnlyList<Remote> _remotes;

        public TestNetwork(IReadOnlyList<Remote> remotes)
        {
            _remotes = remotes;
        }

        public override RemoteCollection Remotes 
            => new TestRemoteCollection(_remotes);
    }
}
