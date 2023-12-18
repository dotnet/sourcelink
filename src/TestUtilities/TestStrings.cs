// Licensed to the.NET Foundation under one or more agreements.
// The.NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

namespace TestUtilities;

public static class TestStrings
{
    /// <summary>
    /// Used for testing repository names.
    /// </summary>
    public const string RepoName = $"test-repo{GB18030}%24%2572%2F";

    /// <summary>
    /// Used for testing repository names. Partially escaped due to https://github.com/dotnet/runtime/issues/89538.
    /// </summary>
    public const string RepoNameEscaped = $"test-repo{GB18030Escaped}%24%2572%2F";

    /// <summary>
    /// Used for testing repository names. Fully escaped.
    /// </summary>
    public const string RepoNameFullyEscaped = $"test-repo{GB18030FullyEscaped}%24%2572%2F";

    /// <summary>
    /// Used for testing domain names.
    /// </summary>
    public const string DomainName = GB18030;

    public const string GB18030 = "𫚭鿯龻蝌灋齅ㄥ﹫䶱ན།ىي꓂";

    /// <summary>
    /// PUA character "" is escaped due to https://github.com/dotnet/runtime/issues/89538.
    /// </summary>
    public const string GB18030Escaped = "%EE%89%9B𫚭鿯龻蝌灋齅ㄥ﹫䶱ན།ىي꓂";

    /// <summary>
    /// All characters are escaped.
    /// </summary>
    public const string GB18030FullyEscaped = "%EE%89%9B%F0%AB%9A%AD%E9%BF%AF%E9%BE%BB%E8%9D%8C%E7%81%8B%E9%BD%85%E3%84%A5%EF%B9%AB%E4%B6%B1%E0%BD%93%E0%BC%8D%D9%89%D9%8A%EA%93%82";
}
