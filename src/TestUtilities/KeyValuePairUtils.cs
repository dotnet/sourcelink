// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.Collections.Generic;

namespace TestUtilities
{
    public static class KeyValuePairUtils
    {
        public static KeyValuePair<T1, T2> KVP<T1, T2>(T1 item1, T2 item2)
            => new KeyValuePair<T1, T2>(item1, item2);

        public static void Deconstruct<T1, T2>(this KeyValuePair<T1, T2> kvp, out T1 item1, out T2 item2)
        {
            item1 = kvp.Key;
            item2 = kvp.Value;
        }
    }
}
