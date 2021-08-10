// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

#if NET461

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.Build.Tasks.Git
{
    internal static class AssemblyResolver
    {
        private static readonly string s_taskDirectory = Path.GetDirectoryName(typeof(AssemblyResolver).Assembly.Location);
        private static readonly List<string> s_loaderLog = new List<string>();

        public static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
        }

        private static void Log(ResolveEventArgs args, string outcome)
        {
            lock (s_loaderLog)
            {
                s_loaderLog.Add($"Loading '{args.Name}' referenced by '{args.RequestingAssembly}': {outcome}.");
            }
        }

        internal static string[] GetLog()
        {
            lock (s_loaderLog)
            {
                return s_loaderLog.ToArray();
            }
        }

        private static Assembly? AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);

            if (!name.Name.Equals("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fullPath = Path.Combine(s_taskDirectory, "System.Collections.Immutable.dll");

            Assembly sci;
            try
            {
                sci = Assembly.LoadFile(fullPath);
            }
            catch (Exception e)
            {
                Log(args, $"exception while loading '{fullPath}': {e.Message}");
                return null;
            }

            if (name.Version <= sci.GetName().Version)
            {
                Log(args, $"loaded '{fullPath}' to {AppDomain.CurrentDomain.FriendlyName}");
                return sci;
            }

            return null;
        }
    }
}

#endif
