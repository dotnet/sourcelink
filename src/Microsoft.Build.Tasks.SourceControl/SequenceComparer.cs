// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks
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

        public int Compare(T[] left, T[] right)
            => Compare(left, left.Length, right, right.Length);
    }
}
