﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;

namespace Microsoft.Build.Tasks.Git
{
    internal readonly struct GitVariableName : IEquatable<GitVariableName>
    {
        public static readonly StringComparer SectionNameComparer = StringComparer.OrdinalIgnoreCase;
        public static readonly StringComparer SubsectionNameComparer = StringComparer.Ordinal;
        public static readonly StringComparer VariableNameComparer = StringComparer.OrdinalIgnoreCase;

        public readonly string SectionName;
        public readonly string SubsectionName;
        public readonly string VariableName;

        public GitVariableName(string sectionName, string subsectionName, string variableName)
        {
            Debug.Assert(sectionName != null);
            Debug.Assert(subsectionName != null);
            Debug.Assert(variableName != null);

            SectionName = sectionName;
            SubsectionName = subsectionName;
            VariableName = variableName;
        }

        public bool SectionNameEquals(string name)
            => SectionNameComparer.Equals(SectionName, name);

        public bool SubsectionNameEquals(string name)
            => SubsectionNameComparer.Equals(SubsectionName, name);

        public bool VariableNameEquals(string name)
            => VariableNameComparer.Equals(VariableName, name);

        public bool Equals(GitVariableName other)
            => SectionNameEquals(other.SectionName) &&
               SubsectionNameEquals(other.SubsectionName) &&
               VariableNameEquals(other.VariableName);

        public override bool Equals(object obj)
            => obj is GitVariableName other && Equals(other);

        public override int GetHashCode()
            => SectionName.GetHashCode() ^ SubsectionName.GetHashCode() ^ VariableName.GetHashCode();

        public override string ToString()
            => (SubsectionName.Length == 0) ?
                SectionName + "." + VariableName :
                SectionName + "." + SubsectionName + "." + VariableName;
    }
}
