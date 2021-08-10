// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

// Implementation based on documentation:
// - https://git-scm.com/docs/gitignore
// - http://man7.org/linux/man-pages/man7/glob.7.html
// - https://research.swtch.com/glob

using System;
using System.Diagnostics;

namespace Microsoft.Build.Tasks.Git
{
    // https://github.com/dotnet/corefx/issues/18922
    // https://github.com/dotnet/corefx/issues/25873

    internal static class Glob
    {
        internal static bool IsMatch(string pattern, string path, bool ignoreCase, bool matchWildCardWithDirectorySeparator)
        {
            int patternIndex = 0;
            int pathIndex = 0;

            // true if the next matching character must be the first character of a directory name
            bool matchDirectoryNameStart = false;

            bool stopAtPathSlash = false;

            int nextSinglePatternIndex = -1;
            int nextSinglePathIndex = -1;
            int nextDoublePatternIndex = -1;
            int nextDoublePathIndex = -1;

            bool equal(char x, char y)
                => x == y || ignoreCase && char.ToLowerInvariant(x) == char.ToLowerInvariant(y);

            while (patternIndex < pattern.Length)
            {
                var c = pattern[patternIndex++];
                if (c == '*')
                {
                    // "a/**/*" does not match "a", although it might appear from the spec that it should.

                    bool isDoubleAsterisk = patternIndex < pattern.Length && pattern[patternIndex] == '*' && 
                                            (patternIndex == pattern.Length - 1 || pattern[patternIndex + 1] == '/') && 
                                            (patternIndex == 1 || pattern[patternIndex - 2] == '/');

                    if (isDoubleAsterisk)
                    {
                        // trailing "/**"
                        if (patternIndex == pattern.Length - 1)
                        {
                            // remaining path definitely matches
                            return true;
                        }

                        // At this point the initial '/' (if any) is already matched.
                        Debug.Assert(pattern[patternIndex] == '*' && pattern[patternIndex + 1] == '/');

                        // Continue matching remainder of the pattern following "**/" with the remainder of the path.
                        // The next path character only matches if it is preceded by '/'.
                        // Consider the following cases
                        //   "**/a*" ~ "abc"          
                        //   "**/b" ~ "x/yb/b"       (do not match the first 'b', only the second one)
                        //   "**/?" ~ "x/yz/u"       (do not match 'y', 'z'; match 'u')
                        //   "a/**/b*" ~ "a/bcd"
                        //   "a/**/b" ~ "a/x/yb/b"   (do not match the first 'b', only the second one)
                        patternIndex += 2;

                        stopAtPathSlash = false;
                        matchDirectoryNameStart = true;
                    }
                    else
                    {
                        // trailing "*"
                        if (patternIndex == pattern.Length)
                        {
                            return matchWildCardWithDirectorySeparator || path.IndexOf('/', pathIndex) == -1;
                        }

                        stopAtPathSlash = !matchWildCardWithDirectorySeparator;
                        matchDirectoryNameStart = false;
                    }

                    // If the rest of the pattern fails to match the rest of the path, we restart matching at the following indices.
                    // A sequence of consecutive resets is effectively searching the path for a substring that matches the span of the pattern
                    // in between the current wildcard and the next one.
                    //
                    // For example, consider matching pattern "A/**/B/**/C/*.D" to path "A/z/u/B/q/r/C/z.D".
                    // Processing the first ** wildcard keeps resetting until the pattern is alligned with "/B/" in the path (wildcard matches "z/u").
                    // Processing the next ** wildcard keeps resetting until the pattern is alligned with "/C/" in the path (wildcard matches "q/r").
                    // Finally, processing the * wildcard aligns on ".D" and the wildcard matches "z".
                    //
                    // If ** and * differ in matching '/' (matchWildCardWithDirectorySeparator is false) we need to reset them independently. 
                    // Consider pattern "A/**/B*C*D/**/E" matching to "A/u/v/BaaCaaX/u/BoCoD/u/E".
                    // If we aligned on substring "/B" in between the first ** and the next * we would not match the path correctly.
                    // Instead, we need to align on the sub-pattern "/B*C*D/" in between the first and the second **. 
                    if (stopAtPathSlash)
                    {
                        nextSinglePatternIndex = patternIndex;
                        nextSinglePathIndex = pathIndex + 1;
                    }
                    else
                    {
                        nextDoublePatternIndex = patternIndex;
                        nextDoublePathIndex = pathIndex + 1;
                    }

                    continue;
                }

                bool matching;

                if (c == '?')
                {
                    // "?" matches any character except for "/" (when matchWildCardWithDirectorySeparator is false)
                    matching = pathIndex < path.Length && (matchWildCardWithDirectorySeparator || path[pathIndex] != '/');
                }
                else if (c == '[')
                {
                    // "[]" matches a single character in the range
                    matching = pathIndex < path.Length && IsRangeMatch(pattern, ref patternIndex, path[pathIndex], ignoreCase, matchWildCardWithDirectorySeparator);
                }
                else 
                {
                    if (c == '\\' && patternIndex < pattern.Length)
                    {
                        c = pattern[patternIndex++];
                    }

                    // match specific character:
                    matching = pathIndex < path.Length && equal(c, path[pathIndex]);
                }

                if (matching && (!matchDirectoryNameStart || pathIndex == 0 || path[pathIndex - 1] == '/'))
                {
                    matchDirectoryNameStart = false;
                    pathIndex++;
                }
                else if (nextDoublePatternIndex >= 0 || nextSinglePatternIndex >= 0)
                {
                    // mismatch while matching pattern following a wildcard ** or *

                    // "*" matches anything but "/" (when matchWildCardWithDirectorySeparator is false)
                    if (!stopAtPathSlash || pathIndex < path.Length && path[pathIndex] == '/')
                    {
                        // Reset to the last saved ** position, if any. 
                        // Also handles reset of * when matchWildCardWithDirectorySeparator is true.

                        if (nextDoublePatternIndex < 0)
                        {
                            return false;
                        }

                        patternIndex = nextDoublePatternIndex;
                        pathIndex = nextDoublePathIndex;

                        nextDoublePathIndex++;
                    }
                    else
                    {
                        // Reset to the last saved * position.

                        patternIndex = nextSinglePatternIndex;
                        pathIndex = nextSinglePathIndex;

                        nextSinglePathIndex++;
                    }

                    Debug.Assert(patternIndex >= 0);
                    Debug.Assert(pathIndex >= 0);

                    if (pathIndex >= path.Length)
                    {
                        return false;
                    }
                }
                else
                {
                    // character mismatch
                    return false;
                }
            }

            return pathIndex == path.Length;
        }

        private static bool IsRangeMatch(string pattern, ref int patternIndex, char pathChar, bool ignoreCase, bool matchWildCardWithDirectorySeparator)
        {
            Debug.Assert(pattern[patternIndex - 1] == '[');

            if (patternIndex == pattern.Length)
            {
                return false;
            }

            if (ignoreCase)
            {
                pathChar = char.ToLowerInvariant(pathChar);
            }

            bool negate = false;
            bool isEmpty = true;
            bool isMatching = false;

            var c = pattern[patternIndex];
            if (c == '!' || c == '^')
            {
                negate = true;
                patternIndex++;
            }

            while (patternIndex < pattern.Length)
            {
                c = pattern[patternIndex++];
                if (c == ']' && !isEmpty)
                {
                    // Range does not match '/', but [^a] matches '/' if matchWildCardWithDirectorySeparator=true.
                    return (pathChar != '/' || matchWildCardWithDirectorySeparator) && (negate ? !isMatching : isMatching);
                }

                if (ignoreCase)
                {
                    c = char.ToLowerInvariant(c);
                }

                char d;

                if (patternIndex + 1 < pattern.Length && pattern[patternIndex] == '-' && (d = pattern[patternIndex + 1]) != ']')
                {
                    if (ignoreCase)
                    {
                        d = char.ToLowerInvariant(d);
                    }

                    isMatching |= pathChar == c || pathChar > c && pathChar <= d;
                    patternIndex += 2;
                }
                else
                {
                    // continue parsing to validate the range is well-formed
                    isMatching |= pathChar == c;
                }

                isEmpty = false;
            }

            // malformed range
            return false;
        }
    }
}
