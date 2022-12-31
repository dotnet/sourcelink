// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed partial class GitIgnore
    {
        internal sealed class PatternGroup
        {
            /// <summary>
            /// Directory of the .gitignore file that defines the pattern.
            /// Full posix slash terminated path.
            /// </summary>
            public readonly string ContainingDirectory;

            public readonly ImmutableArray<Pattern> Patterns;

            public readonly PatternGroup? Parent;

            public PatternGroup(PatternGroup? parent, string containingDirectory, ImmutableArray<Pattern> patterns)
            {
                NullableDebug.Assert(PathUtils.IsPosixPath(containingDirectory));
                NullableDebug.Assert(PathUtils.HasTrailingSlash(containingDirectory));

                Parent = parent;
                ContainingDirectory = containingDirectory;
                Patterns = patterns;
            }
        }

        internal readonly struct Pattern
        {
            public readonly PatternFlags Flags;
            public readonly string Glob;

            public Pattern(string glob,  PatternFlags flags)
            {
                Glob = glob;
                Flags = flags;
            }

            public bool IsDirectoryPattern => (Flags & PatternFlags.DirectoryPattern) != 0;
            public bool IsFullPathPattern => (Flags & PatternFlags.FullPath) != 0;
            public bool IsNegative => (Flags & PatternFlags.Negative) != 0;

            public override string ToString() 
                => $"{(IsNegative ? "!" : "")}{Glob}{(IsDirectoryPattern ? " <dir>" : "")}{(IsFullPathPattern ? " <path>" : "")}";
        }

        [Flags]
        internal enum PatternFlags
        {
            None = 0,
            Negative = 1,
            DirectoryPattern = 2,
            FullPath = 4,
        }

        private const string GitIgnoreFileName = ".gitignore";

        /// <summary>
        /// Full posix slash terminated path.
        /// </summary>
        public string WorkingDirectory { get; }
        private readonly string _workingDirectoryNoSlash;

        public bool IgnoreCase { get; }

        public PatternGroup? Root { get; }

        internal GitIgnore(PatternGroup? root, string workingDirectory, bool ignoreCase)
        {
            NullableDebug.Assert(PathUtils.IsAbsolute(workingDirectory));

            IgnoreCase = ignoreCase;
            WorkingDirectory = PathUtils.ToPosixDirectoryPath(workingDirectory);
            _workingDirectoryNoSlash = PathUtils.TrimTrailingSlash(WorkingDirectory);
            Root = root;
        }

        private StringComparison PathComparison
            => IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

        private IEqualityComparer<string> PathComparer
            => IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

        public Matcher CreateMatcher()
            => new Matcher(this);

        /// <exception cref="IOException"/>
        /// <exception cref="ArgumentException"><paramref name="path"/> is invalid</exception>
        internal static PatternGroup? LoadFromFile(string? path, PatternGroup? parent)
        {
            // See https://git-scm.com/docs/gitignore#_pattern_format

            if (!File.Exists(path))
            {
                return null;
            }

            StreamReader reader;
            try
            {
                reader = File.OpenText(path!);
            }
            catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException)
            {
                return null;
            }

            var reusableBuffer = new StringBuilder();

            var directory = PathUtils.ToPosixDirectoryPath(Path.GetFullPath(Path.GetDirectoryName(path)!));
            var patterns = ImmutableArray.CreateBuilder<Pattern>();

            using (reader)
            {
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    if (TryParsePattern(line, reusableBuffer, out var glob, out var flags))
                    {
                        patterns.Add(new Pattern(glob, flags));
                    }
                }
            }

            if (patterns.Count == 0)
            {
                return null;
            }

            return new PatternGroup(parent, directory, patterns.ToImmutable());
        }

        internal static bool TryParsePattern(string line, StringBuilder reusableBuffer, [NotNullWhen(true)]out string? glob, out PatternFlags flags)
        {
            glob = null;
            flags = PatternFlags.None;
            
            // Trailing spaces are ignored unless '\'-escaped.
            // Leading spaces are significant.
            // Other whitespace (\t, \v, \f) is significant. 
            int e = line.Length - 1;
            while (e >= 0 && line[e] == ' ')
            {
                e--;
            }

            e++;

            // Skip blank line.
            if (e == 0)
            {
                return false;
            }

            // put trailing space back if escaped:
            if (e < line.Length && line[e] == ' ' && line[e - 1] == '\\')
            {
                e++;
            }

            int s = 0;

            // Skip comment.
            if (line[s] == '#')
            {
                return false;
            }

            // Pattern negation.
            if (line[s] == '!')
            {
                flags |= PatternFlags.Negative;
                s++;
            }

            if (s == e)
            {
                return false;
            }

            if (line[e - 1] == '/')
            {
                flags |= PatternFlags.DirectoryPattern;
                e--;
            }

            if (s == e)
            {
                return false;
            }

            if (line.IndexOf('/', s, e - s) >= 0)
            {
                flags |= PatternFlags.FullPath;
            }

            if (line[s] == '/')
            {
                s++;
            }

            if (s == e)
            {
                return false;
            }

            int escape = line.IndexOf('\\', s, e - s);
            if (escape < 0)
            {
                glob = line.Substring(s, e - s);
                return true;
            }

            reusableBuffer.Clear();
            reusableBuffer.Append(line, s, escape - s);

            int i = escape;
            while (i < e)
            {
                var c = line[i++];
                if (c == '\\' && i < e)
                {
                    c = line[i++];
                }

                reusableBuffer.Append(c);
            }

            glob = reusableBuffer.ToString();
            return true;
        }
    }
}
