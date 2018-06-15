// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
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
