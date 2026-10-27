// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;

namespace Microsoft.Build.Tasks.Git;

internal readonly struct GitVariableName(string sectionName, string subsectionName, string variableName) : IEquatable<GitVariableName>
{
    public static readonly StringComparer SectionNameComparer = StringComparer.OrdinalIgnoreCase;
    public static readonly StringComparer SubsectionNameComparer = StringComparer.Ordinal;
    public static readonly StringComparer VariableNameComparer = StringComparer.OrdinalIgnoreCase;

    public readonly string SectionName = sectionName;
    public readonly string SubsectionName = subsectionName;
    public readonly string VariableName = variableName;

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

    public override bool Equals(object? obj)
        => obj is GitVariableName other && Equals(other);

    public override int GetHashCode()
        => SectionNameComparer.GetHashCode(SectionName) ^
           SubsectionNameComparer.GetHashCode(SubsectionName) ^
           VariableNameComparer.GetHashCode(VariableName);

    public override string ToString()
        => (SubsectionName.Length == 0) ?
            SectionName + "." + VariableName :
            SectionName + "." + SubsectionName + "." + VariableName;
}
