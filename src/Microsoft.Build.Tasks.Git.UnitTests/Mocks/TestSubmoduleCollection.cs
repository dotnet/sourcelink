// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestSubmoduleCollection : SubmoduleCollection
    {
        private readonly IReadOnlyList<Submodule> _submodules;

        public TestSubmoduleCollection(IReadOnlyList<Submodule> submodules)
        {
            _submodules = submodules;
        }

        public override IEnumerator<Submodule> GetEnumerator()
            => _submodules.GetEnumerator();
    }
}
