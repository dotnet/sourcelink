// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed partial class GitConfig
    {
        public static readonly GitConfig Empty = new GitConfig(ImmutableDictionary<VariableKey, ImmutableArray<string>>.Empty);

        internal readonly struct VariableKey : IEquatable<VariableKey>
        {
            public static readonly StringComparer SectionNameComparer = StringComparer.OrdinalIgnoreCase;
            public static readonly StringComparer SubsectionNameComparer = StringComparer.Ordinal;
            public static readonly StringComparer VariableNameComparer = StringComparer.OrdinalIgnoreCase;

            public readonly string SectionName;
            public readonly string SubsectionName;
            public readonly string VariableName;

            public VariableKey(string sectionName, string subsectionName, string variableName)
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

            public bool Equals(VariableKey other)
                => SectionNameEquals(other.SectionName) &&
                   SubsectionNameEquals(other.SubsectionName) &&
                   VariableNameEquals(other.VariableName);

            public override bool Equals(object obj)
                => obj is VariableKey other && Equals(other);

            public override int GetHashCode()
                => SectionName.GetHashCode() ^ SubsectionName.GetHashCode() ^ VariableName.GetHashCode();

            public override string ToString()
                => (SubsectionName.Length == 0) ?
                    SectionName + "." + VariableName :
                    SectionName + "." + SubsectionName + "." + VariableName;
        }

        public readonly ImmutableDictionary<VariableKey, ImmutableArray<string>> Variables;

        public GitConfig(ImmutableDictionary<VariableKey, ImmutableArray<string>> variables)
        {
            Debug.Assert(variables != null);
            Variables = variables;
        }

        // for testing:
        internal IEnumerable<KeyValuePair<string, ImmutableArray<string>>> EnumerateVariables()
            => Variables.Select(kvp => new KeyValuePair<string, ImmutableArray<string>>(kvp.Key.ToString(), kvp.Value));

        public ImmutableArray<string> GetVariableValues(string section, string name)
            => GetVariableValues(section, subsection: "", name);

        public ImmutableArray<string> GetVariableValues(string section, string subsection, string name)
            => Variables.TryGetValue(new VariableKey(section, subsection, name), out var multiValue) ? multiValue : default;

        public string GetVariableValue(string section, string name)
            => GetVariableValue(section, "", name);

        public string GetVariableValue(string section, string subsection, string name)
        {
            var values = GetVariableValues(section, subsection, name);
            return values.IsDefault ? null : values[values.Length - 1];
        }

        public static bool ParseBooleanValue(string str, bool defaultValue = false)
            => TryParseBooleanValue(str, out var value) ? value : defaultValue;

        public static bool TryParseBooleanValue(string str, out bool value)
        {
            // https://git-scm.com/docs/git-config#Documentation/git-config.txt-boolean

            if (str == null)
            {
                value = false;
                return false;
            }

            var comparer = StringComparer.OrdinalIgnoreCase;

            if (str == "1" || comparer.Equals(str, "true") || comparer.Equals(str, "on") || comparer.Equals(str, "yes"))
            {
                value = true;
                return true;
            }

            if (str == "0" || comparer.Equals(str, "false") || comparer.Equals(str, "off") || comparer.Equals(str, "no") || str == "")
            {
                value = false;
                return true;
            }

            value = false;
            return false;
        }

        internal static long ParseInt64Value(string str, long defaultValue = 0)
            => TryParseInt64Value(str, out var value) ? value : defaultValue;

        internal static bool TryParseInt64Value(string str, out long value)
        {
            if (string.IsNullOrEmpty(str))
            {
                value = 0;
                return false;
            }

            long multiplier;
            switch (str[str.Length - 1])
            {
                case 'K':
                case 'k':
                    multiplier = 1024;
                    break;

                case 'M':
                case 'm':
                    multiplier = 1024 * 1024;
                    break;

                case 'G':
                case 'g':
                    multiplier = 1024 * 1024 * 1024;
                    break;

                default:
                    multiplier = 1;
                    break;
            }

            if (!long.TryParse(multiplier > 1 ? str.Substring(0, str.Length - 1) : str, out value))
            {
                return false;
            }

            try
            {
                value = checked(value * multiplier);
            }
            catch (OverflowException)
            {
                return false;
            }

            return true;
        }
    }
}
