// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.
using System;

namespace TestUtilities
{
    /// <summary>
    /// Attribute added to the test assembly during build. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class BuildInfoAttribute : Attribute
    {
        public string SdkVersion { get; }
        public string PackagesDirectory { get; }
        public string LogDirectory { get; }

        public BuildInfoAttribute(string sdkVersion, string packagesDirectory, string logDirectory)
        {
            SdkVersion = sdkVersion;
            PackagesDirectory = packagesDirectory;
            LogDirectory = logDirectory;
        }
    }
}
