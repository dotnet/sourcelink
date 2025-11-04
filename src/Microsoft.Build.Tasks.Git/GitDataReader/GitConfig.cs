// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed partial class GitConfig
    {
        public static readonly GitConfig Empty = new GitConfig(ImmutableDictionary<GitVariableName, ImmutableArray<string>>.Empty);

        private const int SupportedGitRepoFormatVersion = 1;

        public const string CoreSectionName = "core";
        public const string ExtensionsSectionName = "extensions";

        public const string ObjectFormatVariableName = "objectFormat";
        public const string RepositoryFormatVersionVariableName = "repositoryformatversion";

        private static readonly ImmutableArray<string> s_knownExtensions = ["noop", "preciousObjects", "partialclone", "worktreeConfig", ObjectFormatVariableName];

        public readonly ImmutableDictionary<GitVariableName, ImmutableArray<string>> Variables;

        /// <summary>
        /// The parsed value of "extensions.objectFormat" variable.
        /// </summary>
        public ObjectFormat ObjectFormat { get; }

        /// <exception cref="InvalidDataException"/>
        public GitConfig(ImmutableDictionary<GitVariableName, ImmutableArray<string>> variables)
        {
            NullableDebug.Assert(variables != null);
            Variables = variables;
            
            ObjectFormat = GetVariableValue(ExtensionsSectionName, ObjectFormatVariableName) is { } objectFormatStr 
                ? ParseObjectFormat(objectFormatStr)
                : ObjectFormat.Sha1;
        }

        /// <exception cref="NotSupportedException"/>
        internal void ValidateFormat()
        {
            // See https://github.com/git/git/blob/master/Documentation/technical/repository-version.txt
            string? versionStr = GetVariableValue(CoreSectionName, RepositoryFormatVersionVariableName);
            if (TryParseInt64Value(versionStr, out var version) && version > SupportedGitRepoFormatVersion)
            {
                throw new NotSupportedException(string.Format(Resources.UnsupportedRepositoryVersion, versionStr, SupportedGitRepoFormatVersion));
            }

            if (version == 1)
            {
                // All variables defined under extensions section must be known, otherwise a git implementation is not allowed to proced.
                foreach (var variable in Variables)
                {
                    if (variable.Key.SectionNameEquals(ExtensionsSectionName) &&
                        !s_knownExtensions.Contains(variable.Key.VariableName, StringComparer.OrdinalIgnoreCase))
                    {
                        throw new NotSupportedException(string.Format(
                            Resources.UnsupportedRepositoryExtension, variable.Key.VariableName, string.Join(", ", s_knownExtensions)));
                    }
                }
            }
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

        // internal for testing
        internal static ObjectFormat ParseObjectFormat(string value)
            => value switch
            {
                "sha1" => ObjectFormat.Sha1,
                "sha256" => ObjectFormat.Sha256,
                _ => throw new InvalidDataException(),
            };
    }
}
