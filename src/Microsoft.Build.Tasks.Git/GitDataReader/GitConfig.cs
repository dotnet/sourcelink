// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed partial class GitConfig
    {
        public static readonly GitConfig Empty = new GitConfig(ImmutableDictionary<GitVariableName, ImmutableArray<string>>.Empty);

        public readonly ImmutableDictionary<GitVariableName, ImmutableArray<string>> Variables;

        public GitConfig(ImmutableDictionary<GitVariableName, ImmutableArray<string>> variables)
        {
            NullableDebug.Assert(variables != null);
            Variables = variables;
        }

        // for testing:
        internal IEnumerable<KeyValuePair<string, ImmutableArray<string>>> EnumerateVariables()
            => Variables.Select(kvp => new KeyValuePair<string, ImmutableArray<string>>(kvp.Key.ToString(), kvp.Value));

        public ImmutableArray<string> GetVariableValues(string section, string name)
            => GetVariableValues(section, subsection: "", name);

        public ImmutableArray<string> GetVariableValues(string section, string subsection, string name)
            => Variables.TryGetValue(new GitVariableName(section, subsection, name), out var multiValue) ? multiValue : default;

        public string? GetVariableValue(string section, string name)
            => GetVariableValue(section, "", name);

        public string? GetVariableValue(string section, string subsection, string name)
        {
            var values = GetVariableValues(section, subsection, name);
            return values.IsDefault ? null : values[values.Length - 1];
        }

        public static bool ParseBooleanValue(string? str, bool defaultValue = false)
            => TryParseBooleanValue(str, out var value) ? value : defaultValue;

        public static bool TryParseBooleanValue(string? str, out bool value)
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

        internal static bool TryParseInt64Value(string? str, out long value)
        {
            if (NullableString.IsNullOrEmpty(str))
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
