// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TestUtilities;
using Xunit;

namespace Microsoft.SourceLink.Tools.UnitTests
{
    public class SourceLinkMapTests
    {
        private IEnumerable<string> Inspect(SourceLinkMap map)
            => map.Entries.Select(e => $"('{e.FilePath.Path}', {(e.FilePath.IsPrefix ? "*" : "")}) -> ('{e.Uri.Prefix}', '{e.Uri.Suffix}')");

        [Theory]
        [InlineData(@"{}")]
        [InlineData(@"{""xxx"":{}}")]
        [InlineData(@"{""documents"":{}}")]
        public void Empty(string json)
        {
            var map = SourceLinkMap.Parse(json);
            Assert.Empty(map.Entries);
        }

        [Fact]
        public void Extra()
        {
            var map = SourceLinkMap.Parse(@"
{
   ""documents"" : 
   {
      ""C:\\a*"": ""http://server/1/a*"",
   },
   ""extra"": {}
}");
            AssertEx.Equal(new[] { "('C:\\a', *) -> ('http://server/1/a', '')" }, Inspect(map));
        }

        [Fact]
        public void Entries()
        {
            var map = SourceLinkMap.Parse(@"
{
   ""documents"" : 
   {
      ""C:\\a*"": ""http://server/[*]"",
      ""C:\\a*"": ""http://a/"",
      ""C:\\a"": ""http://a/"",
      ""C:\\a*"": ""http://*a"",
      ""C:\\b"": ""http://b"",
   }
}");
            AssertEx.Equal(new[]
            {
                @"('C:\a', *) -> ('http://server/[', ']')",
                @"('C:\a', *) -> ('http://a/', '')",
                @"('C:\a', ) -> ('http://a/', '')",
                @"('C:\a', *) -> ('http://', 'a')",
                @"('C:\b', ) -> ('http://b', '')"
            }, Inspect(map));

            Assert.True(map.TryGetUri(@"C:\a", out var url));
            Assert.Equal("http://server/[]", url);

            Assert.True(map.TryGetUri(@"C:\a\b\c\d\e", out url));
            Assert.Equal("http://server/[/b/c/d/e]", url);

            Assert.True(map.TryGetUri(@"C:\b", out url));
            Assert.Equal("http://b", url);

            Assert.False(map.TryGetUri(@"C:\b\c", out _));
        }

        [Fact]
        public void Order1()
        {
            var map = SourceLinkMap.Parse(@"
{
   ""documents"" : 
   {
      ""C:\\a\\b*"": ""2:*"",
      ""C:\\a\\b\\c*"": ""1:*"",
      ""C:\\a*"": ""3:*"",
   }
}");
            AssertEx.Equal(new[]
            {
                @"('C:\a\b\c', *) -> ('1:', '')",
                @"('C:\a\b', *) -> ('2:', '')",
                @"('C:\a', *) -> ('3:', '')"
            }, Inspect(map));

            string? url;
            Assert.True(map.TryGetUri(@"C:\a\b\c\d\e", out url));
            Assert.Equal("1:/d/e", url);

            Assert.True(map.TryGetUri(@"C:\a\b\", out url));
            Assert.Equal("2:/", url);

            Assert.True(map.TryGetUri(@"C:\a\x", out url));
            Assert.Equal("3:/x", url);

            Assert.False(map.TryGetUri(@"D:\x", out _));
        }

        [Fact]
        public void Order2()
        {
            var map = SourceLinkMap.Parse(@"
{
   ""documents"" : 
   {
      ""C:\\aaa\\bbb*"": ""1:*"",
      ""C:\\aaa\\bb*"": ""2:*"",
   }
}");
            AssertEx.Equal(new[]
            {
                @"('C:\aaa\bbb', *) -> ('1:', '')",
                @"('C:\aaa\bb', *) -> ('2:', '')",
            }, Inspect(map));

            string? url;
            Assert.True(map.TryGetUri(@"C:\aaa\bbbb", out url));
            Assert.Equal("1:b", url);

            Assert.True(map.TryGetUri(@"C:\aaa\bbb", out url));
            Assert.Equal("1:", url);

            Assert.True(map.TryGetUri(@"C:\aaa\bb", out url));
            Assert.Equal("2:", url);

            Assert.False(map.TryGetUri(@"C:\aaa\b", out _));
        }

        [Fact]
        public void TryGetUrl_Star()
        {
            var map = SourceLinkMap.Parse(@"{""documents"":{}}");
            Assert.False(map.TryGetUri("path*", out _));
        }

        [Fact]
        public void TryGetUrl_InvalidArgument()
        {
            var map = SourceLinkMap.Parse(@"{""documents"":{}}");
            Assert.Throws<ArgumentNullException>(() => map.TryGetUri(null!, out _));
        }

        [Theory]
        [InlineData(@"{")]
        [InlineData(@"{""documents"" : { ""x"": ""y"" // comments not allowed
} }")]
        [InlineData(@"{""documents"" : { 1: ""y"" } }")]
        public void BadJson_Key(string json)
        {
            Assert.ThrowsAny<JsonException>(() => SourceLinkMap.Parse(json));
        }

        [Theory]
        [InlineData(@"1")]
        [InlineData(@"{""documents"": 1}")]
        [InlineData(@"{""documents"":{""1"": 1}}")]
        [InlineData(@"{""documents"":{""1"": null}}")]
        [InlineData(@"{""documents"":{""1"": {}}}")]
        [InlineData(@"{""documents"":{""1"": []}}")]
        public void InvalidJsonTypes(string json)
        {
            Assert.Throws<InvalidDataException>(() => SourceLinkMap.Parse(json));
        }

        [Theory]
        [InlineData(@"{""documents"":{"""": ""x""}}")]
        [InlineData(@"{""documents"":{""1**"": ""x""}}")]
        [InlineData(@"{""documents"":{""*1*"": ""x""}}")]
        [InlineData(@"{""documents"":{""1*"": ""**x""}}")]
        [InlineData(@"{""documents"":{""*1"": ""x*""}}")]
        [InlineData(@"{""documents"":{""1"": ""x*""}}")]
        public void InvalidWildcards(string json)
        {
            Assert.Throws<InvalidDataException>(() => SourceLinkMap.Parse(json));
        }

        [Fact]
        public void Parse_InvalidArgument()
        {
            Assert.Throws<ArgumentNullException>(() => SourceLinkMap.Parse(null!));
        }
    }
}
