// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed class GitReferenceResolver
    {
        // See https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-HEAD

        private const string PackedRefsFileName = "packed-refs";
        private const string RefsPrefix = "refs/";

        private readonly string _commonDirectory;
        private readonly string _gitDirectory;

        // maps refs/heads references to the correspondign object ids:
        private readonly Lazy<ImmutableDictionary<string, string>> _lazyPackedReferences;

        public GitReferenceResolver(string gitDirectory, string commonDirectory)
        {
            Debug.Assert(PathUtils.IsNormalized(gitDirectory));
            Debug.Assert(PathUtils.IsNormalized(commonDirectory));

            _gitDirectory = gitDirectory;
            _commonDirectory = commonDirectory;
            _lazyPackedReferences = new Lazy<ImmutableDictionary<string, string>>(() => ReadPackedReferences(_gitDirectory));
        }

        private static ImmutableDictionary<string, string> ReadPackedReferences(string gitDirectory)
        {
            // https://git-scm.com/docs/git-pack-refs

            var packedRefsPath = Path.Combine(gitDirectory, PackedRefsFileName);
            if (!File.Exists(packedRefsPath))
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            TextReader reader;
            try
            {
                reader = File.OpenText(packedRefsPath);
            }
            catch
            {
                return ImmutableDictionary<string, string>.Empty;
            }

            using (reader)
            {
                return ReadPackedReferences(reader, packedRefsPath);
            }
        }

        // internal for testing
        internal static ImmutableDictionary<string, string> ReadPackedReferences(TextReader reader, string path)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>();

            // header: 
            // "# pack-refs with:" [options]

            var header = reader.ReadLine();
            if (header == null || !header.StartsWith("# pack-refs with:"))
            {
                throw new InvalidDataException($"Expected header not found at the beginning of file '{path}'.");
            }

            string? previousObjectId = null;
            while (true)
            {
                var line = reader.ReadLine();
                if (line == null)
                {
                    return builder.ToImmutable();
                }

                Exception invalidData() => new InvalidDataException($"Invalid packed references specification in '{path}': '{line}'");

                // When the line starts with ^ it specifies an id of the object
                // pointed to by a tag identified on the previous line
                if (line.Length > 0 && line[0] == '^')
                {
                    if (previousObjectId == null)
                    {
                        throw invalidData();
                    }

                    var dereferencedTagObjectId = line.Substring(1);
                    if (!IsObjectId(dereferencedTagObjectId))
                    {
                        throw invalidData();
                    }

                    // currently we don't need the dereferenced tag object id for anything, 
                    // so we just skip the line
                    continue;
                }

                int separator = line.IndexOfAny(CharUtils.WhitespaceSeparators);
                if (separator == -1)
                {
                    throw invalidData();
                }

                var objectId = line.Substring(0, separator);
                if (!IsObjectId(objectId))
                {
                    throw invalidData();
                }

                int nextSeparator = line.IndexOfAny(CharUtils.WhitespaceSeparators, separator + 1);
                var reference = (nextSeparator >= 0) ? line.Substring(separator + 1, nextSeparator - separator - 1) : line.Substring(separator + 1);

                if (reference.Length == 0)
                {
                    throw invalidData();
                }

                previousObjectId = objectId;

                // if any text (even whitespace) follows next whitespace separator the line is ignored
                if (nextSeparator >= 0)
                {
                    continue;
                }

                // we are only interested in references
                if (!reference.StartsWith(RefsPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                // Its not clear how git handles duplicates. Take the first one.
                if (!builder.ContainsKey(reference))
                {
                    builder.Add(reference, objectId);
                }
            }
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        public string? ResolveHeadReference()
            => ResolveReference(ReadReferenceFromFile(Path.Combine(_gitDirectory, GitRepository.GitHeadFileName)));

        public string? ResolveReference(string reference)
        {
            HashSet<string>? lazyVisitedReferences = null;
            return ResolveReference(reference, ref lazyVisitedReferences);
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        private string? ResolveReference(string reference, ref HashSet<string>? lazyVisitedReferences)
        {
            // See https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-HEAD

            const string refPrefix = "ref: ";
            if (reference.StartsWith(refPrefix + RefsPrefix, StringComparison.Ordinal))
            {
                var symRef = reference.Substring(refPrefix.Length);

                if (lazyVisitedReferences != null && !lazyVisitedReferences.Add(symRef))
                {
                    // infinite recursion
                    throw new InvalidDataException(string.Format(Resources.RecursionDetectedWhileResolvingReference, reference));
                }

                string path;
                try
                {
                    path = Path.Combine(_commonDirectory, symRef);
                }
                catch
                {
                    return null;
                }

                if (!File.Exists(path))
                {
                    return ResolvePackedReference(symRef);
                }

                string content;
                try
                {
                    content = ReadReferenceFromFile(path);
                }
                catch (Exception e) when (e is ArgumentException or FileNotFoundException or DirectoryNotFoundException)
                {
                    // invalid path or file doesn't exist:
                    return ResolvePackedReference(symRef);
                }

                if (IsObjectId(content))
                {
                    return content;
                }

                lazyVisitedReferences ??= new HashSet<string>();

                return ResolveReference(content, ref lazyVisitedReferences);
            }

            if (IsObjectId(reference))
            {
                return reference;
            }

            throw new InvalidDataException(string.Format(Resources.InvalidReference, reference));
        }

        /// <exception cref="ArgumentException"/>
        /// <exception cref="IOException"/>
        internal static string ReadReferenceFromFile(string path)
        {
            try
            {
                return File.ReadAllText(path).TrimEnd(CharUtils.AsciiWhitespace);
            }
            catch (Exception e) when (e is not ArgumentException and not IOException)
            {
                throw new IOException(e.Message, e);
            }
        }

        private string? ResolvePackedReference(string reference)
            => _lazyPackedReferences.Value.TryGetValue(reference, out var objectId) ? objectId : null;

        private static bool IsObjectId(string reference)
            => reference.Length == 40 && reference.All(CharUtils.IsHexadecimalDigit);
    }
}
