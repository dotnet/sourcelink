// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

#if NET461

using System.Collections.Generic;

namespace System
{
    internal struct ValueTuple<T1, T2>
    {
        public T1 Item1;
        public T2 Item2;

        public ValueTuple(T1 item1, T2 item2)
        {
            Item1 = item1;
            Item2 = item2;
        }
    }

    internal struct ValueTuple<T1, T2, T3>
    {
        public T1 Item1;
        public T2 Item2;
        public T3 Item3;

        public ValueTuple(T1 item1, T2 item2, T3 item3)
        {
            Item1 = item1;
            Item2 = item2;
            Item3 = item3;
        }
    }

    namespace Runtime.CompilerServices
    {
        internal sealed class TupleElementNamesAttribute : Attribute
        {
            public IList<string> TransformNames { get; }

            public TupleElementNamesAttribute(string[] transformNames)
            {
                TransformNames = transformNames;
            }
        }
    }
}

#endif