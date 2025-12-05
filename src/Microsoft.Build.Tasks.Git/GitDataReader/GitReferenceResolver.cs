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

namespace Microsoft.Build.Tasks.Git
{
    internal sealed class GitReferenceResolver : IDisposable
    {
        // See https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-HEAD

        private const string PackedRefsFileName = "packed-refs";
        private const string TablesListFileName = "tables.list";
        private const string RefsPrefix = "refs/";
        private const string RefTableDirectoryName = "reftable";

        private readonly string _commonDirectory;
        private readonly string _gitDirectory;
        private readonly ReferenceStorageFormat _storageFormat;
        private readonly ObjectNameFormat _objectNameFormat;

        // maps refs/heads references to the correspondign object ids:
        private readonly Lazy<ImmutableDictionary<string, string>> _lazyPackedReferences;
        private readonly Lazy<IEnumerable<GitRefTableReader>> _lazyRefTableReferenceReaders;

        // lock on access:
        private readonly List<GitRefTableReader> _openedRefTableReaders = [];

        public GitReferenceResolver(string gitDirectory, string commonDirectory, ReferenceStorageFormat storageFormat, ObjectNameFormat objectNameFormat)
        {
            Debug.Assert(PathUtils.IsNormalized(gitDirectory));
            Debug.Assert(PathUtils.IsNormalized(commonDirectory));

            _gitDirectory = gitDirectory;
            _commonDirectory = commonDirectory;
            _storageFormat = storageFormat;
            _objectNameFormat = objectNameFormat;
            _lazyPackedReferences = new(() => ReadPackedReferences(_gitDirectory));
            _lazyRefTableReferenceReaders = new(() => CreateRefTableReaders(_gitDirectory, _openedRefTableReaders));
        }

        public void Dispose()
        {
            lock (_openedRefTableReaders)
            {
                foreach (var reader in _openedRefTableReaders)
                {
                    reader.Dispose();
                }

                _openedRefTableReaders.Clear();
            }
        }

        private ImmutableDictionary<string, string> ReadPackedReferences(string gitDirectory)
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
        internal ImmutableDictionary<string, string> ReadPackedReferences(TextReader reader, string path)
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

                    var dereferencedTagObjectId = line[1..];
                    if (!IsObjectId(dereferencedTagObjectId))
                    {
                        throw invalidData();
                    }

                    // currently we don't need the dereferenced tag object id for anything, 
                    // so we just skip the line
                    continue;
                }

                var separator = line.IndexOfAny(CharUtils.WhitespaceSeparators);
                if (separator == -1)
                {
                    throw invalidData();
                }

                var objectId = line[..separator];
                if (!IsObjectId(objectId))
                {
                    throw invalidData();
                }

                var nextSeparator = line.IndexOfAny(CharUtils.WhitespaceSeparators, separator + 1);
                var reference = (nextSeparator >= 0) ? line.Substring(separator + 1, nextSeparator - separator - 1) : line[(separator + 1)..];

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
        private static IEnumerable<GitRefTableReader> CreateRefTableReaders(string gitDirectory, List<GitRefTableReader> openReaders)
        {
            var refTableDirectory = Path.Combine(gitDirectory, RefTableDirectoryName);
            var tablesFilePath = Path.Combine(refTableDirectory, TablesListFileName);

            // Create lazily-evaluated sequence of readers for each entry in the tables.list file (in reverse order).
            // Only evaluate the first one that exists.
            // Reference resolution will open the subsequent files as needed.

            var readers = File.ReadAllLines(tablesFilePath)
                .Where(fileName => fileName.EndsWith(".ref"))
                .Reverse()
                .Select(fileName =>
                {
                    var path = Path.Combine(refTableDirectory, fileName);

                    Stream stream;
                    try
                    {
                        stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                    }
                    catch (FileNotFoundException)
                    {
                        return null;
                    }

                    var reader = new GitRefTableReader(stream);
                    lock (openReaders)
                    {
                        openReaders.Add(reader);
                    }

                    return reader;
                })
                .Where(s => s != null);

            if (!readers.Any())
            {
                throw new InvalidDataException();
            }

            return readers!;
        }

        public string? GetBranchForHead()
            => FindHeadReference().referenceName;

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        public string? ResolveHeadReference()
        {
            var (objectName, referenceName) = FindHeadReference();
            return objectName ?? ResolveReferenceName(referenceName!);
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        public string? ResolveReference(string reference)
        {
            var (objectName, referenceName) = ParseObjectNameOrReference(reference);
            return objectName ?? ResolveReferenceName(referenceName!);
        }

        private static bool TryGetReferenceName(string reference, [NotNullWhen(true)] out string? name)
        {
            const string refPrefix = "ref: ";
            if (reference.StartsWith(refPrefix + RefsPrefix, StringComparison.Ordinal))
            {
                name = reference[refPrefix.Length..];
                return true;
            }

            name = null;
            return false;
        }

        private (string? objectName, string? referenceName) ParseObjectNameOrReference(string value)
        {
            if (TryGetReferenceName(value, out var referenceName))
            {
                return (objectName: null, referenceName);
            }

            if (IsObjectId(value))
            {
                return (value, referenceName: null);
            }

            throw new InvalidDataException(string.Format(Resources.InvalidReference, value));
        }

        /// <exception cref="IOException"/>
        /// <exception cref="InvalidDataException"/>
        private string? ResolveReferenceName(string referenceName)
        {
            HashSet<string>? lazyVisitedReferences = null;
            return Recurse(referenceName);

            string? Recurse(string currentReferenceName)
            {
                // See https://git-scm.com/docs/gitrepository-layout#Documentation/gitrepository-layout.txt-HEAD

                if (lazyVisitedReferences != null && !lazyVisitedReferences.Add(currentReferenceName))
                {
                    // infinite recursion
                    throw new InvalidDataException(string.Format(Resources.RecursionDetectedWhileResolvingReference, referenceName));
                }

                var (objectName, foundReferenceName) = FindReference(currentReferenceName);
                if (objectName != null)
                {
                    return objectName;
                }

                if (foundReferenceName == null)
                {
                    return null;
                }

                lazyVisitedReferences ??= [];
                return Recurse(foundReferenceName);
            }
        }

        private (string? objectName, string? referenceName) FindHeadReference()
            => _storageFormat switch
            {
                ReferenceStorageFormat.LooseFiles => ParseObjectNameOrReference(ReadReferenceFromFile(Path.Combine(_gitDirectory, GitRepository.GitHeadFileName))),
                ReferenceStorageFormat.RefTable => FindReferenceInRefTable(GitRepository.GitHeadFileName),
                _ => throw new InvalidOperationException(),
            };

        private (string? objectName, string? referenceName) FindReference(string referenceName)
            => _storageFormat switch
            {
                ReferenceStorageFormat.LooseFiles => FindReferenceInLooseFile(referenceName),
                ReferenceStorageFormat.RefTable => FindReferenceInRefTable(referenceName),
                _ => throw new InvalidOperationException()
            };

        private (string? objectName, string? referenceName) FindReferenceInRefTable(string referenceName)
        {
            foreach (var reader in _lazyRefTableReferenceReaders.Value)
            {
                if (!reader.TryFindReference(referenceName, out var objectName, out var symbolicReference))
                {
                    continue;
                }

                return (objectName, symbolicReference);
            }

            return default;
        }

        private (string? objectName, string? referenceName) FindReferenceInLooseFile(string referenceName)
        {
            var content = Find() ?? FindPackedReference(referenceName);
            if (content == null)
            {
                return default;
            }

            return ParseObjectNameOrReference(content);

            string? Find()
            {
                string path;
                try
                {
                    path = Path.Combine(_commonDirectory, referenceName);
                }
                catch
                {
                    return null;
                }

                if (!File.Exists(path))
                {
                    return null;
                }

                try
                {
                    return ReadReferenceFromFile(path);
                }
                catch (Exception e) when (e is ArgumentException or FileNotFoundException or DirectoryNotFoundException)
                {
                    // invalid path or file doesn't exist:
                    return null;
                }
            }
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

        private string? FindPackedReference(string reference)
            => _lazyPackedReferences.Value.TryGetValue(reference, out var objectId) ? objectId : null;

        private bool IsObjectId(string reference)
            => reference.Length == _objectNameFormat.HashSize * 2 && reference.All(CharUtils.IsHexadecimalDigit);
    }
}
