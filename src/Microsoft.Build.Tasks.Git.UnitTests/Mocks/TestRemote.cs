// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestRemote : Remote
    {
        private readonly string _name;
        private readonly string _url;
                                
        public TestRemote(string name, string url)
        {
            _name = name;
            _url = url;
        }

        public override string Name => _name;
        public override string Url => _url;
    }
}
