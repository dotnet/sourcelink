// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.SourceControl.UnitTests
{
    public sealed class MockItem : ITaskItem
    {
        public static string AdjustSeparators(string path)
            => Path.DirectorySeparatorChar == '/' ? path.Replace('\\', '/') : path;

        public MockItem(string spec)
        {
            // msbuild normalizes paths on non-Windows like so:
            ItemSpec = AdjustSeparators(spec);
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
            throw new NotImplementedException();
        }

        public void RemoveMetadata(string metadataName)
        {
            throw new NotImplementedException();
        }

        public void SetMetadata(string metadataName, string metadataValue)
        {
            throw new NotImplementedException();
        }
    }
}
