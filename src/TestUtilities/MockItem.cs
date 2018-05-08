// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.SourceLink.Common.UnitTests
{
    public sealed class MockItem : ITaskItem
    {
        private readonly Dictionary<string, string> _metadata = new Dictionary<string, string>();

        public static string AdjustSeparators(string path)
            => Path.DirectorySeparatorChar == '/' ? path.Replace('\\', '/') : path;

        public MockItem(string spec, params KeyValuePair<string, string>[] metadata)
        {
            // msbuild normalizes paths on non-Windows like so:
            ItemSpec = AdjustSeparators(spec);

            foreach (var (name, value) in metadata)
            {
                SetMetadata(name, value);
            }
        }

        public string ItemSpec { get; set; }

        public ICollection MetadataNames => throw new NotImplementedException();

        public int MetadataCount => throw new NotImplementedException();

        public IDictionary CloneCustomMetadata()
        {
            throw new NotImplementedException();
        }

        public void CopyMetadataTo(ITaskItem destinationItem)
        {
            throw new NotImplementedException();
        }

        public string GetMetadata(string metadataName)
        {
            return _metadata.TryGetValue(metadataName, out var value) ? value : null;
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            _metadata[metadataName] = metadataValue;
        }
    }
}
