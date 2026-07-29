// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed partial class GitConfig
    {
        public static readonly GitConfig Empty = new(ImmutableDictionary<GitVariableName, ImmutableArray<string>>.Empty);

        private const int SupportedGitRepoFormatVersion = 1;

        private const string CoreSectionName = "core";
        private const string ExtensionsSectionName = "extensions";

        private const string RefStorageExtensionName = "refstorage";
        private const string ObjectFormatExtensionName = "objectFormat";
        private const string RelativeWorktreesExtensionName = "relativeWorktrees";
        private const string RepositoryFormatVersionVariableName = "repositoryformatversion";

        private static readonly ImmutableArray<string> s_knownExtensions =
            ["noop", "preciousObjects", "partialclone", "worktreeConfig", RefStorageExtensionName, ObjectFormatExtensionName, RelativeWorktreesExtensionName];

        public readonly ImmutableDictionary<GitVariableName, ImmutableArray<string>> Variables;
        public readonly ReferenceStorageFormat ReferenceStorageFormat;

        /// <summary>
        /// The parsed value of "extensions.objectFormat" variable.
        /// </summary>
        public ObjectNameFormat ObjectNameFormat { get; }

        /// <exception cref="InvalidDataException"/>
        public GitConfig(ImmutableDictionary<GitVariableName, ImmutableArray<string>> variables)
        {
            Variables = variables;

            ReferenceStorageFormat = GetVariableValue(ExtensionsSectionName, RefStorageExtensionName) switch
            {
                null => ReferenceStorageFormat.LooseFiles,
                "reftable" => ReferenceStorageFormat.RefTable,
                _ => throw new InvalidDataException(),
            };

            ObjectNameFormat = GetVariableValue(ExtensionsSectionName, ObjectFormatExtensionName) switch
            {
                null or "sha1" => ObjectNameFormat.Sha1,
                "sha256" => ObjectNameFormat.Sha256,
                _ => throw new InvalidDataException(),
            };
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <exception cref="NotSupportedException"/>
        public static GitConfig ReadRepositoryConfig(string gitDirectory, string commonDirectory, GitEnvironment environment)
        {
            var reader = new Reader(gitDirectory, commonDirectory, environment);
            var config = reader.Load();
            config.ValidateRepositoryConfig();
            return config;
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        /// <exception cref="NotSupportedException"/>
        public static GitConfig ReadSubmoduleConfig(string gitDirectory, string commonDirectory, GitEnvironment environment, string submodulesFile)
        {
            var reader = new Reader(gitDirectory, commonDirectory, environment);
            return reader.LoadFrom(submodulesFile);
        }

        private void ValidateRepositoryConfig()
        {
            // See https://github.com/git/git/blob/master/Documentation/technical/repository-version.txt
            var versionStr = GetVariableValue(CoreSectionName, RepositoryFormatVersionVariableName);
            if (TryParseInt64Value(versionStr, out var version) && version > SupportedGitRepoFormatVersion)
            {
                throw new NotSupportedException(string.Format(Resources.UnsupportedRepositoryVersion, versionStr, SupportedGitRepoFormatVersion));
            }

            if (version == 1)
            {
                // All variables defined under extensions section must be known, otherwise a git implementation is not allowed to proceed.
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

            if (!long.TryParse(multiplier > 1 ? str[..^1] : str, out value))
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
