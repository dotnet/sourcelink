// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;
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

            var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path);

            Assert.Equal("0123456789ABCDEFabcdef000000000000000000", resolver.ResolveReference("0123456789ABCDEFabcdef000000000000000000"));

            Assert.Equal("0000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/master"));
            Assert.Equal("0000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/br1"));
            Assert.Equal("0000000000000000000000000000000000000000", resolver.ResolveReference("ref: refs/heads/br2"));

            // branch without commits (emtpy repository) will have not file in refs/heads:
            Assert.Null(resolver.ResolveReference("ref: refs/heads/none"));

            Assert.Null(resolver.ResolveReference("ref: refs/heads/rec1   "));
            Assert.Null(resolver.ResolveReference("ref: refs/heads/none" + string.Join("/", Path.GetInvalidPathChars())));
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

            var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path);

            Assert.Throws<InvalidDataException>(() => resolver.ResolveReference("ref: refs/heads/rec1"));
            Assert.Throws<InvalidDataException>(() => resolver.ResolveReference("ref: xyz/heads/rec1"));
            Assert.Throws<InvalidDataException>(() => resolver.ResolveReference("ref:refs/heads/rec1"));
            Assert.Throws<InvalidDataException>(() => resolver.ResolveReference("refs/heads/rec1"));
            Assert.Throws<InvalidDataException>(() => resolver.ResolveReference(new string('0', 39)));
            Assert.Throws<InvalidDataException>(() => resolver.ResolveReference(new string('0', 41)));
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

            var resolver = new GitReferenceResolver(gitDir.Path, commonDir.Path);

            Assert.Equal("1111111111111111111111111111111111111111", resolver.ResolveReference("ref: refs/heads/master"));
            Assert.Equal("2222222222222222222222222222222222222222", resolver.ResolveReference("ref: refs/heads/br1"));
            Assert.Equal("2222222222222222222222222222222222222222", resolver.ResolveReference("ref: refs/heads/br2"));
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

            var actual = GitReferenceResolver.ReadPackedReferences(new StringReader(packedRefs), "<path>");

            AssertEx.SetEqual(new[]
            {
                "refs/heads/br:2222222222222222222222222222222222222222",
                "refs/heads/master:1111111111111111111111111111111111111111"
            }, actual.Select(e => $"{e.Key}:{e.Value}"));
        }

        [Theory]
        [InlineData("# pack-refs with:")]
        [InlineData("# pack-refs with:xyz")]
        [InlineData("# pack-refs with:xyz\n")]
        public void ReadPackedReferences_Empty(string content)
        {
            Assert.Empty(GitReferenceResolver.ReadPackedReferences(new StringReader(content), "<path>"));
        }

        [Theory]
        [InlineData("")]                                                               // missing header
        [InlineData("# pack-refs with")]                                               // invalid header prefix
        [InlineData("# pack-refs with:xyz\n1")]                                        // bad object id
        [InlineData("# pack-refs with:xyz\n1111111111111111111111111111111111111111")] // no reference name
        [InlineData("# pack-refs with:xyz\n^1111111111111111111111111111111111111111")]      // tag dereference without previous ref
        [InlineData("# pack-refs with:xyz\n1111111111111111111111111111111111111111 x\n^1")] // bad object id
        [InlineData("# pack-refs with:xyz\n^1111111111111111111111111111111111111111\n^2222222222222222222222222222222222222222")] // tag dereference without previous ref
        public void ReadPackedReferences_Errors(string content)
        {
            Assert.Throws<InvalidDataException>(() => GitReferenceResolver.ReadPackedReferences(new StringReader(content), "<path>"));
        }
    }
}
