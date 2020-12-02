// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.SourceLink.Tools
{
    internal static class LanguageGuids
    {
        public static readonly Guid CSharp = new("3f5162f8-07c6-11d3-9053-00c04fa302a1");
        public static readonly Guid FSharp = new("ab4f38c9-b6e6-43ba-be3b-58080b2ccce3");
        public static readonly Guid VisualBasic = new("3a12d0b8-c26c-11d0-b442-00a0244a1dd2");

        public static string GetName(Guid guid)
        {
            if (guid == CSharp) return "C#";
            if (guid == FSharp) return "F#";
            if (guid == VisualBasic) return "VB";
            return guid.ToString();
        }
    }
}
