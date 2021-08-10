// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Tasks.SourceControl
{
    internal sealed class SequenceComparer<T> : IComparer<T[]>
        where T : IComparable<T>
    {
        private readonly IComparer<T> _comparer;

        public SequenceComparer(IComparer<T> comparer)
        {
            _comparer = comparer;
        }

        public bool StartsWith(T[] sequence, T[] prefix)
          => (prefix.Length <= sequence.Length) && Compare(sequence, prefix.Length, prefix, prefix.Length) == 0;

        public int Compare(T[] left, int leftLength, T[] right, int rightLength)
        {
            var minLength = Math.Min(leftLength, rightLength);
            for (int i = 0; i < minLength; i++)
            {
                var result = _comparer.Compare(left[i], right[i]);
                if (result != 0)
                {
                    return result;
                }
            }

            return leftLength.CompareTo(rightLength);
        }

        public int Compare([AllowNull] T[] left, [AllowNull] T[] right)
        {
            left ??= Array.Empty<T>();
            right ??= Array.Empty<T>();
            return Compare(left, left.Length, right, right.Length);
        }
    }
}
