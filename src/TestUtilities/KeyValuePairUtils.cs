// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.SourceControl.UnitTests
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
