// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.Build.Tasks.Git
{
    internal static class TaskImplementation
    {
        public static Func<LocateRepository, bool> LocateRepository;
        public static Func<GetRepositoryUrl, bool> GetRepositoryUrl;
        public static Func<GetSourceRevisionId, bool> GetSourceRevisionId;
        public static Func<GetSourceRoots, bool> GetSourceRoots;
        public static Func<GetUntrackedFiles, bool> GetUntrackedFiles;

        private static readonly string s_taskDirectory;
        private const string GitOperationsAssemblyName = "Microsoft.Build.Tasks.Git.Operations";

        static TaskImplementation()
        {
            s_taskDirectory = Path.GetDirectoryName(typeof(TaskImplementation).Assembly.Location);
#if NET461
            s_nullVersion = new Version(0, 0, 0, 0);
            s_loaderLog = new List<string>();

            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;

            var assemblyName = typeof(TaskImplementation).Assembly.GetName();
            assemblyName.Name = GitOperationsAssemblyName;
            var assembly = Assembly.Load(assemblyName);
#else
            var operationsPath = Path.Combine(s_taskDirectory, GitOperationsAssemblyName + ".dll");
            var assembly = GitLoaderContext.Instance.LoadFromAssemblyPath(operationsPath);
#endif
            var type = assembly.GetType("Microsoft.Build.Tasks.Git.RepositoryTasks", throwOnError: true).GetTypeInfo();

            LocateRepository = (Func<LocateRepository, bool>)type.GetDeclaredMethod(nameof(LocateRepository)).CreateDelegate(typeof(Func<LocateRepository, bool>));
            GetRepositoryUrl = (Func<GetRepositoryUrl, bool>)type.GetDeclaredMethod(nameof(GetRepositoryUrl)).CreateDelegate(typeof(Func<GetRepositoryUrl, bool>));
            GetSourceRevisionId = (Func<GetSourceRevisionId, bool>)type.GetDeclaredMethod(nameof(GetSourceRevisionId)).CreateDelegate(typeof(Func<GetSourceRevisionId, bool>));
            GetSourceRoots = (Func<GetSourceRoots, bool>)type.GetDeclaredMethod(nameof(GetSourceRoots)).CreateDelegate(typeof(Func<GetSourceRoots, bool>));
            GetUntrackedFiles = (Func<GetUntrackedFiles, bool>)type.GetDeclaredMethod(nameof(GetUntrackedFiles)).CreateDelegate(typeof(Func<GetUntrackedFiles, bool>));
        }

#if NET461
        private static readonly Version s_nullVersion;
        private static readonly List<string> s_loaderLog;

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

        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Limit resolution scope to minimum to affect the rest of msbuild as little as possible.
            // Only resolve System.* assemblies from the task directory that are referenced with 0.0.0.0 version (from netstandard.dll).

            var referenceName = new AssemblyName(args.Name);
            if (!referenceName.Name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))
            {
                Log(args, "not System");
                return null;
            }

            if (referenceName.Version != s_nullVersion)
            {
                Log(args, "not null version");
                return null;
            }

            var referencePath = Path.Combine(s_taskDirectory, referenceName.Name + ".dll");
            if (!File.Exists(referencePath))
            {
                Log(args, $"file '{referencePath}' not found");
                return null;
            }

            Log(args, $"loading from '{referencePath}'");
            return Assembly.Load(AssemblyName.GetAssemblyName(referencePath));
        }
#endif
    }
}
