// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Tasks.Git;

internal static class CollectionExtensions
{
    internal static (TValue? exactMatch, int firstGreater) BinarySearch<TItem, TValue>(
        this IReadOnlyList<TItem> array,
        int index,
        int length,
        Func<TItem, TValue> selector,
        Func<TValue, int> compareItemToSearchValue)
    {
        var lo = index;
        var hi = index + length - 1;
        while (lo <= hi)
        {
            var i = lo + ((hi - lo) >> 1);
            var item = selector(array[i]);

            var comparison = compareItemToSearchValue(item);
            if (comparison == 0)
            {
                return (exactMatch: item, firstGreater: -1);
            }

            if (comparison < 0)
            {
                lo = i + 1;
            }
            else
            {
                hi = i - 1;
            }
        }

        return (exactMatch: default, firstGreater: lo);
    }
}
