// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using TestUtilities;

namespace Microsoft.SourceLink.IntegrationTests
{
    internal static class TestUtilities
    {
        public static void ValidateAssemblyInformationalVersion(string assembylPath, string version)
        {
            var assembly = Assembly.LoadFile(assembylPath);
            var aiva = assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().Single();
            AssertEx.AreEqual(version, aiva.InformationalVersion);
        }

        public static void ValidateNuSpecRepository(string nuspecPath, string type, string commit, string url)
        {
            using var archive = new ZipArchive(File.OpenRead(nuspecPath));
            using var nuspecStream = archive.GetEntry("test.nuspec")!.Open();

            var nuspec = XDocument.Load(nuspecStream);
            var repositoryNode = nuspec.Descendants(XName.Get("repository", "http://schemas.microsoft.com/packaging/2012/06/nuspec.xsd"));
            AssertEx.AreEqual(type, repositoryNode.Attributes("type").Single().Value);
            AssertEx.AreEqual(commit, repositoryNode.Attributes("commit").Single().Value);
            AssertEx.AreEqual(url, repositoryNode.Attributes("url").Single().Value);
        }
    }
}
