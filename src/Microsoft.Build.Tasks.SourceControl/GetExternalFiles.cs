// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    // TODO: move to Microsoft.Build.Tasks
    public class GetExternalFiles : Task
    {
        [Required]
        public ITaskItem[] Files { get; set; }

        [Required]
        public ITaskItem[] Directories { get; set; }

        [Output]
        public ITaskItem[] ExternalFiles { get; set; }

        public override bool Execute()
        {
            // TODO: Path handling 
            // Can we cache full path for each TaskItem?
            // The Compile items are relative to the project file. We can work with relative paths directly without normalizing them.
            // Handle invalid paths.
            ExternalFiles = GetExternal(Files, Directories, d => Path.GetFullPath(d.ItemSpec)).ToArray();
            return true;
        }

        internal static IEnumerable<T> GetExternal<T>(T[] files, T[] directories, Func<T, string> getPath)
        {
            var comparer = new SequenceComparer<string>(Path.DirectorySeparatorChar == '\\' ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            var roots = directories.Select(d => PathUtilities.Split(getPath(d))).OrderBy(d => d, comparer).ToArray();

            return files.Where(file =>
            {
                var dir = PathUtilities.Split(Path.GetDirectoryName(getPath(file)));

                int index = Array.BinarySearch(roots, dir, comparer);
                if (index >= 0)
                {
                    return false;
                }

                var root = roots[(~index > 0) ? ~index - 1 : ~index];
                if (comparer.StartsWith(dir, root))
                {
                    return false;
                }

                return true;
            });
        }
    }
}
