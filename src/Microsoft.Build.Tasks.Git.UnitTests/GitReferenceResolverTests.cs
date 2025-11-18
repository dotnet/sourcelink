// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System.IO;
using System.Linq;
using TestUtilities;
using Xunit;

namespace Microsoft.Build.Tasks.Git.UnitTests
{
    public class GitReferenceResolverTests
    {
        [Fact]
        public void ResolveReference()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();

            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");

            refsHeadsDir.CreateFile("master").WriteAllText("0000000000000000000000000000000000000000");
            refsHeadsDir.CreateFile("br1").WriteAllText("ref: refs/heads/br2");
            refsHeadsDir.CreateFile("br2").WriteAllText("ref: refs/heads/master");

            using var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha1);

            Assert.Equal("0123456789ABCDEFabcdef000000000000000000", resolver.ResolveReference("0123456789ABCDEFabcdef000000000000000000"));

            Assert.Equal("0000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/master"));
            Assert.Equal("0000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/br1"));
            Assert.Equal("0000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/br2"));

            // branch without commits (empty repository) will have no file in refs/heads:
            Assert.Null(resolver.ResolveReference("ref: refs/heads/none"));

            Assert.Null(resolver.ResolveReference("ref: refs/heads/rec1   "));
            Assert.Null(resolver.ResolveReference("ref: refs/heads/none" + string.Join("/", Path.GetInvalidPathChars())));
        }

        [Fact]
        public void ResolveReference_SHA256()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();

            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");

            // SHA256 hash (64 characters)
            refsHeadsDir.CreateFile("master").WriteAllText("0000000000000000000000000000000000000000000000000000000000000000");
            refsHeadsDir.CreateFile("br1").WriteAllText("ref: refs/heads/br2");
            refsHeadsDir.CreateFile("br2").WriteAllText("ref: refs/heads/master");

            using var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha256);

            // Verify SHA256 hash is accepted directly
            Assert.Equal(
                "0123456789ABCDEFabcdef000000000000000000000000000000000000000000",
                resolver.ResolveReference("0123456789ABCDEFabcdef000000000000000000000000000000000000000000"));

            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/master"));
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/br1"));
            Assert.Equal("0000000000000000000000000000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/br2"));
        }

        [Fact]
        public void ResolveReference_Errors()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();

            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");

            refsHeadsDir.CreateFile("rec1").WriteAllText("ref: refs/heads/rec2");
            refsHeadsDir.CreateFile("rec2").WriteAllText("ref: refs/heads/rec1");

            using var resolver1 = new GitReferenceResolver(gitDir.Path, commonDir.Path, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha1);

            Assert.Throws<InvalidDataException>(() => resolver1.ResolveReference("ref: refs/heads/rec1"));
            Assert.Throws<InvalidDataException>(() => resolver1.ResolveReference("ref: xyz/heads/rec1"));
            Assert.Throws<InvalidDataException>(() => resolver1.ResolveReference("ref:refs/heads/rec1"));
            Assert.Throws<InvalidDataException>(() => resolver1.ResolveReference("refs/heads/rec1"));

            // Invalid SHA1 hash lengths
            Assert.Throws<InvalidDataException>(() => resolver1.ResolveReference(new string('0', ObjectNameFormat.Sha1.HashSize * 2 - 1)));
            Assert.Throws<InvalidDataException>(() => resolver1.ResolveReference(new string('0', ObjectNameFormat.Sha1.HashSize * 2 + 1)));
            Assert.Throws<InvalidDataException>(() => resolver1.ResolveReference(new string('0', ObjectNameFormat.Sha256.HashSize * 2)));

            using var resolver2 = new GitReferenceResolver(gitDir.Path, commonDir.Path, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha256);

            // Invalid SHA256 hash lengths
            Assert.Throws<InvalidDataException>(() => resolver2.ResolveReference(new string('0', ObjectNameFormat.Sha256.HashSize * 2 - 1)));
            Assert.Throws<InvalidDataException>(() => resolver2.ResolveReference(new string('0', ObjectNameFormat.Sha256.HashSize * 2 + 1)));
            Assert.Throws<InvalidDataException>(() => resolver2.ResolveReference(new string('0', ObjectNameFormat.Sha1.HashSize * 2)));
        }

        [Fact]
        public void ResolveReference_Packed()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();

            gitDir.CreateFile("packed-refs").WriteAllText(
@"# pack-refs with: peeled fully-peeled sorted
1111111111111111111111111111111111111111 refs/heads/master
2222222222222222222222222222222222222222 refs/heads/br2
");
            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");

            refsHeadsDir.CreateFile("br1").WriteAllText("ref: refs/heads/br2");

            using var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha1);

            Assert.Equal("1111111111111111111111111111111111111111", resolver.ResolveReference("ref: refs/heads/master"));
            Assert.Equal("2222222222222222222222222222222222222222", resolver.ResolveReference("ref: refs/heads/br1"));
            Assert.Equal("2222222222222222222222222222222222222222", resolver.ResolveReference("ref: refs/heads/br2"));
        }

        [Fact]
        public void ResolveReference_Packed_SHA256()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();

            // Packed refs with SHA256 hashes (64 characters)
            gitDir.CreateFile("packed-refs").WriteAllText(
@"# pack-refs with: peeled fully-peeled sorted
1111111111111111111111111111111111111111111111111111111111111111 refs/heads/master
2222222222222222222222222222222222222222222222222222222222222222 refs/heads/br2
");
            var commonDir = temp.CreateDirectory();
            var refsHeadsDir = commonDir.CreateDirectory("refs").CreateDirectory("heads");

            refsHeadsDir.CreateFile("br1").WriteAllText("ref: refs/heads/br2");

            using var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha256);

            Assert.Equal("1111111111111111111111111111111111111111111111111111111111111111", resolver.ResolveReference("ref: refs/heads/master"));
            Assert.Equal("2222222222222222222222222222222222222222222222222222222222222222", resolver.ResolveReference("ref: refs/heads/br1"));
            Assert.Equal("2222222222222222222222222222222222222222222222222222222222222222", resolver.ResolveReference("ref: refs/heads/br2"));
        }

        [Fact]
        public void ReadPackedReferences()
        {
            var packedRefs =
@"# pack-refs with:
1111111111111111111111111111111111111111 refs/heads/master
2222222222222222222222222222222222222222 refs/heads/br
^3333333333333333333333333333333333333333
4444444444444444444444444444444444444444 x
5555555555555555555555555555555555555555 y 
6666666666666666666666666666666666666666 y z
7777777777777777777777777777777777777777 refs/heads/br
";

            using var resolver = new GitReferenceResolver(TempRoot.Root, TempRoot.Root, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha1);
            var actual = resolver.ReadPackedReferences(new StringReader(packedRefs), "<path>");

            AssertEx.SetEqual(new[]
            {
                "refs/heads/br:2222222222222222222222222222222222222222",
                "refs/heads/master:1111111111111111111111111111111111111111"
            }, actual.Select(e => $"{e.Key}:{e.Value}"));
        }

        [Fact]
        public void ReadPackedReferences_SHA256()
        {
            var packedRefs =
@"# pack-refs with:
1111111111111111111111111111111111111111111111111111111111111111 refs/heads/master
2222222222222222222222222222222222222222222222222222222222222222 refs/heads/br
^3333333333333333333333333333333333333333333333333333333333333333
4444444444444444444444444444444444444444444444444444444444444444 x
5555555555555555555555555555555555555555555555555555555555555555 y 
6666666666666666666666666666666666666666666666666666666666666666 y z
7777777777777777777777777777777777777777777777777777777777777777 refs/heads/br
";

            using var resolver = new GitReferenceResolver(TempRoot.Root, TempRoot.Root, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha256);
            var actual = resolver.ReadPackedReferences(new StringReader(packedRefs), "<path>");

            AssertEx.SetEqual(new[]
            {
                "refs/heads/br:2222222222222222222222222222222222222222222222222222222222222222",
                "refs/heads/master:1111111111111111111111111111111111111111111111111111111111111111"
            }, actual.Select(e => $"{e.Key}:{e.Value}"));
        }

        [Theory]
        [InlineData("# pack-refs with:")]
        [InlineData("# pack-refs with:xyz")]
        [InlineData("# pack-refs with:xyz\n")]
        public void ReadPackedReferences_Empty(string content)
        {
            using var resolver = new GitReferenceResolver(TempRoot.Root, TempRoot.Root, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha256);
            Assert.Empty(resolver.ReadPackedReferences(new StringReader(content), "<path>"));
        }

        [Theory]
        [InlineData("")]                                                               // missing header
        [InlineData("# pack-refs with")]                                               // invalid header prefix
        [InlineData("# pack-refs with:xyz\n1")]                                        // bad object id
        [InlineData("# pack-refs with:xyz\n^2222222222222222222222222222222222222222222222222222222222222222")] // bad object id: sha256
        [InlineData("# pack-refs with:xyz\n1111111111111111111111111111111111111111")] // no reference name
        [InlineData("# pack-refs with:xyz\n^1111111111111111111111111111111111111111")]      // tag dereference without previous ref
        [InlineData("# pack-refs with:xyz\n1111111111111111111111111111111111111111 x\n^1")] // bad object id
        [InlineData("# pack-refs with:xyz\n^1111111111111111111111111111111111111111\n^2222222222222222222222222222222222222222")] // tag dereference without previous ref
        public void ReadPackedReferences_Errors(string content)
        {
            using var resolver = new GitReferenceResolver(TempRoot.Root, TempRoot.Root, ReferenceStorageFormat.LooseFiles, ObjectNameFormat.Sha1);
            Assert.Throws<InvalidDataException>(() => resolver.ReadPackedReferences(new StringReader(content), "<path>"));
        }

        [Fact]
        public void ResolveReference_RefTable()
        {
            using var temp = new TempRoot();

            var gitDir = temp.CreateDirectory();
            var commonDir = temp.CreateDirectory();

            var refTableDir = gitDir.CreateDirectory("reftable");

            refTableDir.CreateFile("tables.list").WriteAllText("""
                2.ref
                1.ref
                """);

            var ref1 = refTableDir.CreateFile("1.ref").WriteAllBytes(GitRefTableTestWriter.GetRefTableBlob([("refs/heads/a", 0x01), ("refs/heads/c", 0x02)]));
            TempFile ref2;

            using (var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path, ReferenceStorageFormat.RefTable, ObjectNameFormat.Sha1))
            {
                Assert.Equal("0100000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/a"));
                Assert.Equal("0200000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/c"));

                // 2.ref shouldn't be opened until needed:
                ref2 = refTableDir.CreateFile("2.ref").WriteAllBytes(GitRefTableTestWriter.GetRefTableBlob([("refs/heads/b", 0x03), ("refs/heads/c", 0x04)]));

                Assert.Equal("0300000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/b"));
                Assert.Null(resolver.ResolveReference("ref: refs/heads/d"));
            }

            // files should have been closed:
            File.WriteAllBytes(ref1.Path, [0]);
            File.WriteAllBytes(ref2.Path, [0]);
        }
    }
}
