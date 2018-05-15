// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.
#if !NET461
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Microsoft.Build.Tasks.Git
{
    internal sealed class GitLoaderContext : AssemblyLoadContext
    {
        public static readonly GitLoaderContext Instance = new GitLoaderContext();

        protected override Assembly Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == "LibGit2Sharp")
            {
                var path = Path.Combine(Path.GetDirectoryName(typeof(TaskImplementation).Assembly.Location), assemblyName.Name + ".dll");
                return LoadFromAssemblyPath(path);
            }

            return Default.LoadFromAssemblyName(assemblyName);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var modulePtr = IntPtr.Zero;

            if (unmanagedDllName.StartsWith("git2-", StringComparison.Ordinal) ||
                unmanagedDllName.StartsWith("libgit2-", StringComparison.Ordinal))
            {
                var directory = GetNativeLibraryDirectory();
                var extension = GetNativeLibraryExtension();

                if (!unmanagedDllName.EndsWith(extension, StringComparison.Ordinal))
                {
                    unmanagedDllName += extension;
                }

                var nativeLibraryPath = Path.Combine(directory, unmanagedDllName);
                if (!File.Exists(nativeLibraryPath))
                {
                    nativeLibraryPath = Path.Combine(directory, "lib" + unmanagedDllName);
                }

                modulePtr = LoadUnmanagedDllFromPath(nativeLibraryPath);
            }

            return (modulePtr != IntPtr.Zero) ? modulePtr : base.LoadUnmanagedDll(unmanagedDllName);
        }

        internal static string GetNativeLibraryDirectory()
        {
            var dir = Path.GetDirectoryName(typeof(GitLoaderContext).Assembly.Location);
            return Path.Combine(dir, "runtimes", GetNativeLibraryRuntimeId(), "native");
        }

        /// <summary>
        /// Determines the RID to use when loading libgit2 native library.
        /// This method only supports RIDs that are currently used by LibGit2Sharp.NativeBinaries.
        /// </summary>
        private static string GetNativeLibraryRuntimeId()
        {
            var processorArchitecture = IntPtr.Size == 8 ? "x64" : "x86";

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win7-" + processorArchitecture;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return "osx";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return "linux-" + processorArchitecture;
            }

            throw new PlatformNotSupportedException();
        }

        private static string GetNativeLibraryExtension()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return ".dll";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return ".dylib";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return ".so";
            }

            throw new PlatformNotSupportedException();
        }
    }
}
#endif