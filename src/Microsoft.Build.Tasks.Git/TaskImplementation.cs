// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
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
        private static Version s_nullVersion = new Version(0, 0, 0, 0);

        static TaskImplementation()
        {
            s_taskDirectory = Path.GetDirectoryName(typeof(TaskImplementation).Assembly.Location);
#if NET461
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
        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // only resolve dependencies of netstandard library:
            if (!StringComparer.OrdinalIgnoreCase.Equals(args.RequestingAssembly.FullName, "netstandard, Version=2.0.0.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"))
            {
                return null;
            }

            var referenceName = new AssemblyName(AppDomain.CurrentDomain.ApplyPolicy(args.Name));
            if (referenceName.Version != s_nullVersion)
            {
                return null;
            }

            // resolve dependencies in the task's directory:
            var referencePath = Path.Combine(s_taskDirectory, referenceName.Name + ".dll");
            return File.Exists(referencePath) ? Assembly.Load(referencePath) : null;
        }
#endif
    }
}
