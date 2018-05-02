// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using LibGit2Sharp;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    internal class TestConfiguration : Configuration
    {
        private readonly IReadOnlyDictionary<string, object> _values;

        public TestConfiguration(IReadOnlyDictionary<string, object> values)
        {
            _values = values;
        }

        public override T GetValueOrDefault<T>(string key)
        {
            if (!_values.TryGetValue(key, out var obj))
            {
                return default;
            }

            if (obj is T value)
            {
                return value;
            }

            throw new LibGit2SharpException();
        }
    }
}