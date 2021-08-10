// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.TeamFoundation.VersionControl.Client;

namespace Microsoft.Build.Tasks.Tfvc
{
    public class LocateRepository : Task
    {
        [Required, NotNull]
        public string? Directory { get; set; }

        [Output]
        public string? Id { get; set; }

#if UNUSED
        [Output]
        public string Root { get; set; }

        private static int GetLongestCommonPrefix(string[] path1, int length1, string[] path2)
        {
            int length = Math.Min(length1, path2.Length);
            for (int i = 0; i < length; i++)
            {
                if (!string.Equals(path1[i], path2[i], StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return length;
        }
#endif
        public override bool Execute()
        {            
            var workspaceInfo = Workstation.Current.GetLocalWorkspaceInfo(Directory);
            if (workspaceInfo == null || workspaceInfo.MappedPaths.Length == 0)
            {
                Log.LogError($"Unable to locate repository containing directory '{Directory}'.");
                return false;
            }

            var paths = workspaceInfo.MappedPaths;
           
            // any mapping works as an id:
            Id = paths[0];

#if UNUSED
            string NormalizeResult(string path)
                => Path.GetFullPath(path).EndWithSeparator();

            if (paths.Length == 1)
            {
                Root = NormalizeResult(paths[0]);
                return true;
            }

            var parts = PathUtilities.Split(Path.GetFullPath(paths[0]));
            int prefixLength = parts.Length;
            for (int i = 1; i < paths.Length; i++)
            {
                prefixLength = Math.Min(prefixLength, GetLongestCommonPrefix(parts, prefixLength, PathUtilities.Split(Path.GetFullPath(paths[i]))));
            }

            if (prefixLength == 0)
            {
                Root = null;
            }
            else
            {
                Root = NormalizeResult(string.Join(Path.DirectorySeparatorChar.ToString(), parts, 0, prefixLength));
            }
#endif
            return true;
        }
    }
}
