// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.Build.Tasks.Git
{
    partial class GitConfig
    {
        internal class Reader
        {
            private const int MaxIncludeDepth = 10;

            // reused for parsing names
            private readonly StringBuilder _reusableBuffer = new StringBuilder();

            // slash terminated posix path
            private readonly string _gitDirectoryPosix;

            private readonly string _commonDirectory;
            private readonly Func<string, TextReader> _fileOpener;
            private readonly GitEnvironment _environment;

            public Reader(string gitDirectory, string commonDirectory, GitEnvironment environment, Func<string, TextReader>? fileOpener = null)
            {
                NullableDebug.Assert(environment != null);

                _environment = environment;
                _gitDirectoryPosix = PathUtils.ToPosixDirectoryPath(gitDirectory);
                _commonDirectory = commonDirectory;
                _fileOpener = fileOpener ?? File.OpenText;
            }

            /// <exception cref="IOException"/>
            /// <exception cref="InvalidDataException"/>
            /// <exception cref="NotSupportedException"/>
            internal GitConfig Load()
            {
                var variables = new Dictionary<GitVariableName, List<string>>();

                foreach (var path in EnumerateExistingConfigurationFiles())
                {
                    LoadVariablesFrom(path, variables, includeDepth: 0);
                }

                return new GitConfig(variables.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray()));
            }

            /// <exception cref="IOException"/>
            /// <exception cref="InvalidDataException"/>
            /// <exception cref="NotSupportedException"/>
            internal GitConfig LoadFrom(string path)
            {
                var variables = new Dictionary<GitVariableName, List<string>>();
                LoadVariablesFrom(path, variables, includeDepth: 0);
                return new GitConfig(variables.ToImmutableDictionary(kvp => kvp.Key, kvp => kvp.Value.ToImmutableArray()));
            }

            private string? GetXdgDirectory()
            {
                var xdgConfigHome = _environment.XdgConfigHomeDirectory;
                if (xdgConfigHome != null)
                {
                    return Path.Combine(xdgConfigHome, "git");
                }
                
                if (_environment.HomeDirectory != null)
                {
                    return Path.Combine(_environment.HomeDirectory, ".config", "git");
                }

                return null;
            }

            internal IEnumerable<string> EnumerateExistingConfigurationFiles()
            {
                // program data (Windows only)
                if (_environment.ProgramDataDirectory != null)
                {
                    var programDataConfig = Path.Combine(_environment.ProgramDataDirectory, "git", "config");
                    if (File.Exists(programDataConfig))
                    {
                        yield return programDataConfig;
                    }
                }

                // system
                var systemDir = GetSystemConfigurationDirectory();
                if (systemDir != null)
                {
                    var systemConfig = Path.Combine(systemDir, "gitconfig");
                    if (File.Exists(systemConfig))
                    {
                        yield return systemConfig;
                    }
                }

                // XDG
                var xdgDir = GetXdgDirectory();
                if (xdgDir != null)
                {
                    var xdgConfig = Path.Combine(xdgDir, "config");
                    if (File.Exists(xdgConfig))
                    {
                        yield return xdgConfig;
                    }
                }

                // global (user home)
                if (_environment.HomeDirectory != null)
                {
                    var globalConfig = Path.Combine(_environment.HomeDirectory, ".gitconfig");
                    if (File.Exists(globalConfig))
                    {
                        yield return globalConfig;
                    }
                }

                // local
                var localConfig = Path.Combine(_commonDirectory, "config");
                if (File.Exists(localConfig))
                {
                    yield return localConfig;
                }

                // TODO: https://github.com/dotnet/sourcelink/issues/303 
                // worktree config
            }

            private string? GetSystemConfigurationDirectory()
            {
                if (_environment.SystemDirectory == null)
                {
                    return null;
                }

                if (!PathUtils.IsUnixLikePlatform)
                {
                    // Git for Windows stores gitconfig under [install dir]\mingw64\etc,
                    // but other Git Windows implementations use [install dir]\etc.
                    var mingwEtc = Path.Combine(_environment.SystemDirectory, "..", "mingw64", "etc");
                    if (Directory.Exists(mingwEtc))
                    {
                        return mingwEtc;
                    }
                }

                return _environment.SystemDirectory;
            }

            /// <exception cref="IOException"/>
            /// <exception cref="InvalidDataException"/>
            internal void LoadVariablesFrom(string path, Dictionary<GitVariableName, List<string>> variables, int includeDepth)
            {
                // https://git-scm.com/docs/git-config#_syntax

                // The following is allowed:
                //   [section][section]var = x
                //   [section]#[section]

                if (includeDepth > MaxIncludeDepth)
                {
                    throw new InvalidDataException(string.Format(Resources.ConfigurationFileRecursionExceededMaximumAllowedDepth, MaxIncludeDepth));
                }

                TextReader reader;

                try
                {
                    reader = _fileOpener(path);
                }
                catch (Exception e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
                {
                    return;
                }
                catch (Exception e) when (!(e is IOException))
                {
                    throw new IOException(e.Message, e);
                }

                using (reader)
                {
                    string sectionName = "";
                    string subsectionName = "";

                    while (true)
                    {
                        SkipMultilineWhitespace(reader);

                        int c = reader.Peek();
                        if (c == -1)
                        {
                            break;
                        }

                        // Comment to the end of the line:
                        if (IsCommentStart(c))
                        {
                            ReadToLineEnd(reader);
                            continue;
                        }

                        if (c == '[')
                        {
                            ReadSectionHeader(reader, _reusableBuffer, out sectionName, out subsectionName);
                            continue;
                        }

                        ReadVariableDeclaration(reader, _reusableBuffer, out var variableName, out var variableValue);

                        // Variable declared outside of a section is allowed (has no section name prefix).

                        var key = new GitVariableName(sectionName, subsectionName, variableName);
                        if (!variables.TryGetValue(key, out var values))
                        {
                            variables.Add(key, values = new List<string>());
                        }

                        values.Add(variableValue);

                        // Spec https://git-scm.com/docs/git-config#_includes:
                        if (IsIncludePath(key, path))
                        {
                            string includedConfigPath = NormalizeRelativePath(relativePath: variableValue, basePath: path, key);
                            LoadVariablesFrom(includedConfigPath, variables, includeDepth + 1);
                        }
                    }
                }
            }

            /// <exception cref="InvalidDataException"/>
            private string NormalizeRelativePath(string relativePath, string basePath, GitVariableName key)
            {
                string root;
                if (relativePath.Length >= 2 && relativePath[0] == '~' && PathUtils.IsDirectorySeparator(relativePath[1]))
                {
                    root = _environment.GetHomeDirectoryForPathExpansion(relativePath);
                    relativePath = relativePath.Substring(2);
                }
                else
                {
                    root = Path.GetDirectoryName(basePath) ?? "";
                }

                try
                {
                    return Path.GetFullPath(Path.Combine(root, relativePath));
                }
                catch
                {
                    throw new InvalidDataException(string.Format(Resources.ValueOfIsNotValidPath, key.ToString(), relativePath));
                }
            }


            private bool IsIncludePath(GitVariableName key, string configFilePath)
            {
                // unconditional:
                if (key.Equals(new GitVariableName("include", "", "path")))
                {
                    return true;
                }

                // conditional:
                if (GitVariableName.SectionNameComparer.Equals(key.SectionName, "includeIf") &&
                    GitVariableName.VariableNameComparer.Equals(key.VariableName, "path") &&
                    key.SubsectionName != "")
                {
                    bool ignoreCase;
                    string pattern;

                    const string caseSensitiveGitDirPrefix = "gitdir:";
                    const string caseInsensitiveGitDirPrefix = "gitdir/i:";

                    if (key.SubsectionName.StartsWith(caseSensitiveGitDirPrefix, StringComparison.Ordinal))
                    {
                        pattern = key.SubsectionName.Substring(caseSensitiveGitDirPrefix.Length);
                        ignoreCase = false;
                    }
                    else if (key.SubsectionName.StartsWith(caseInsensitiveGitDirPrefix, StringComparison.Ordinal))
                    {
                        pattern = key.SubsectionName.Substring(caseInsensitiveGitDirPrefix.Length);
                        ignoreCase = true;
                    }
                    else
                    {
                        return false;
                    }

                    if (pattern.Length >= 2 && pattern[0] == '.' && pattern[1] == '/')
                    {
                        // leading './' is substituted with the path to the directory containing the current config file.
                        pattern = PathUtils.CombinePosixPaths(PathUtils.ToPosixPath(Path.GetDirectoryName(configFilePath)!), pattern.Substring(2));
                    }
                    else if (pattern.Length >= 2 && pattern[0] == '~' && pattern[1] == '/')
                    {
                        // leading '~/' is substituted with HOME path
                        pattern = PathUtils.CombinePosixPaths(PathUtils.ToPosixPath(_environment.GetHomeDirectoryForPathExpansion(pattern)), pattern.Substring(2));
                    }
                    else if (!PathUtils.IsAbsolute(pattern))
                    {
                        pattern = "**/" + pattern;
                    }

                    if (pattern[pattern.Length - 1] == '/')
                    {
                        pattern += "**";
                    }

                    return Glob.IsMatch(pattern, _gitDirectoryPosix, ignoreCase, matchWildCardWithDirectorySeparator: true);
                }

                return false;
            }

            // internal for testing
            internal static void ReadSectionHeader(TextReader reader, StringBuilder reusableBuffer, out string name, out string subsectionName)
            {
                var nameBuilder = reusableBuffer.Clear();

                int c = reader.Read();
                Debug.Assert(c == '[');

                while (true)
                {
                    c = reader.Read();
                    if (c == ']')
                    {
                        name = nameBuilder.ToString();
                        subsectionName = "";
                        break;
                    }

                    if (IsWhitespace(c))
                    {
                        name = nameBuilder.ToString();
                        subsectionName = ReadSubsectionName(reader, reusableBuffer);

                        c = reader.Read();
                        if (c != ']')
                        {
                            throw new InvalidDataException();
                        }

                        break;
                    }

                    if (IsAlphaNumeric(c) || c == '-' || c == '.')
                    {
                        // Allowed characters: alpha-numeric, '-', '.'; no restriction on the name start character.
                        nameBuilder.Append((char)c);
                    }
                    else
                    {
                        throw new InvalidDataException();
                    }
                }

                name = name.ToLowerInvariant();

                // Deprecated syntax: [section.subsection]
                int firstDot = name.IndexOf('.');
                if (firstDot != -1)
                {
                    // "[.x]" parses to section "", subsection ".x" (lookup ".x.var" suceeds, ".X.var" fails)
                    // "[..x]" parses to section ".", subsection "x" (lookup "..x.var" suceeds, "..X.var" fails)
                    // "[x.]" parses to section "x.", subsection "" (lookups "X..var" and "x..var" suceed)
                    // "[x..]" parses to section "x", subsection "." (lookups "X...var" and "x...var" suceed)

                    var prefix = (firstDot == name.Length - 1) ? name : name.Substring(0, firstDot);
                    var suffix = name.Substring(firstDot + 1);

                    subsectionName = (subsectionName.Length > 0) ? suffix + "." + subsectionName : suffix;
                    name = prefix;
                }
            }

            private static string ReadSubsectionName(TextReader reader, StringBuilder reusableBuffer)
            {
                SkipWhitespace(reader);

                int c = reader.Read();
                if (c != '"')
                {
                    throw new InvalidDataException();
                }

                var subsectionName = reusableBuffer.Clear();
                while (true)
                {
                    c = reader.Read();
                    if (c <= 0)
                    {
                        throw new InvalidDataException();
                    }

                    if (c == '"')
                    {
                        return subsectionName.ToString();
                    }

                    // Escaping: backslashes are skipped.
                    // Section headers can't span multiple lines.
                    if (c == '\\')
                    {
                        c = reader.Read();
                        if (c <= 0)
                        {
                            throw new InvalidDataException();
                        }
                    }

                    subsectionName.Append((char)c);
                }
            }

            // internal for testing
            internal static void ReadVariableDeclaration(TextReader reader, StringBuilder reusableBuffer, out string name, out string value)
            {
                name = ReadVariableName(reader, reusableBuffer);
                if (name.Length == 0)
                {
                    throw new InvalidDataException();
                }

                SkipWhitespace(reader);

                // Not allowed:
                // name         #
                // = value

                int c = reader.Peek();
                if (c == -1 || IsCommentStart(c) || IsEndOfLine(c))
                {
                    ReadToLineEnd(reader);

                    // If the value is not specified the variable is considered of type Boolean with value "true"
                    value = "true";
                    return;
                }

                if (c != '=')
                {
                    throw new InvalidDataException();
                }

                reader.Read();

                SkipWhitespace(reader);

                value = ReadVariableValue(reader, reusableBuffer);
            }

            private static string ReadVariableName(TextReader reader, StringBuilder reusableBuffer)
            {
                var nameBuilder = reusableBuffer.Clear();
                int c;

                // Allowed characters: alpha-numeric, '-'; starts with alphabetic.
                while (IsAlphabetic(c = reader.Peek()) || (c == '-' || IsNumeric(c)) && nameBuilder.Length > 0)
                {
                    nameBuilder.Append((char)c);
                    reader.Read();
                }

                return nameBuilder.ToString().ToLowerInvariant();
            }

            private static string ReadVariableValue(TextReader reader, StringBuilder reusableBuffer)
            {
                // Allowed:
                //   name = "a"x"b"        `axb`
                //   name = "b"#"a"        `b`
                //   name = \
                //          abc            `abc`
                //   name = "a\
                //    bc"                  `a bc`
                //   name = a\
                //   bc                    `abc`
                //   name = a\
                //    bc                   `a bc`

                // read until comment/eoln, quote
                bool inQuotes = false;
                var builder = reusableBuffer.Clear();
                int lengthIgnoringTrailingWhitespace = 0;

                while (true)
                {
                    int c = reader.Read();
                    if (c == -1 || IsEndOfLine(c))
                    {
                        if (inQuotes)
                        {
                            throw new InvalidDataException();
                        }

                        break;
                    }

                    if (c == '\\')
                    {
                        switch (reader.Peek())
                        {
                            case '\r':
                            case '\n':
                                ReadToLineEnd(reader);
                                continue;

                            case 'n':
                                reader.Read();
                                builder.Append('\n');

                                // escaped \n is not considered trailing whitespace:
                                lengthIgnoringTrailingWhitespace = builder.Length;
                                continue;

                            case 't':
                                reader.Read();
                                builder.Append('\t');

                                // escaped \t is not considered trailing whitespace:
                                lengthIgnoringTrailingWhitespace = builder.Length;
                                continue;

                            case '\\':
                            case '"':
                                builder.Append((char)reader.Read());
                                lengthIgnoringTrailingWhitespace = builder.Length;
                                continue;

                            default:
                                throw new InvalidDataException();
                        }
                    }

                    if (c == '"')
                    {
                        inQuotes = !inQuotes;
                        continue;
                    }

                    if (IsCommentStart(c) && !inQuotes)
                    {
                        ReadToLineEnd(reader);
                        break;
                    }

                    builder.Append((char)c);

                    if (!IsWhitespace(c) || inQuotes)
                    {
                        lengthIgnoringTrailingWhitespace = builder.Length;
                    }
                }

                return builder.ToString(0, lengthIgnoringTrailingWhitespace);
            }

            private static void SkipMultilineWhitespace(TextReader reader)
            {
                while (IsWhitespaceOrEndOfLine(reader.Peek()))
                {
                    reader.Read();
                }
            }

            private static void SkipWhitespace(TextReader reader)
            {
                while (IsWhitespace(reader.Peek()))
                {
                    reader.Read();
                }
            }

            private static void ReadToLineEnd(TextReader reader)
            {
                while (true)
                {
                    int c = reader.Read();
                    if (c == -1)
                    {
                        return;
                    }

                    if (c == '\r')
                    {
                        if (reader.Peek() == '\n')
                        {
                            reader.Read();
                            return;
                        }

                        return;
                    }

                    if (c == '\n')
                    {
                        return;
                    }
                }
            }

            private static bool IsCommentStart(int c)
                => c == ';' || c == '#';

            private static bool IsAlphabetic(int c)
                => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z';

            private static bool IsNumeric(int c)
                => c >= '0' && c <= '9';

            private static bool IsAlphaNumeric(int c)
                => IsAlphabetic(c) || IsNumeric(c);

            private static bool IsWhitespace(int c)
                => c == ' ' || c == '\t' || c == '\f' || c == '\v';

            private static bool IsEndOfLine(int c)
                => c == '\r' || c == '\n';

            private static bool IsWhitespaceOrEndOfLine(int c)
                => IsWhitespace(c) || IsEndOfLine(c);
        }
    }
}
