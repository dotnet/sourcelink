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

        static TaskImplementation()
        {
#if NET461
            var assemblyName = typeof(TaskImplementation).Assembly.GetName();
            assemblyName.Name = "Microsoft.Build.Tasks.Git.Operations";
            var assembly = Assembly.Load(assemblyName);
#else
            var operationsPath = Path.Combine(Path.GetDirectoryName(typeof(TaskImplementation).Assembly.Location), "Microsoft.Build.Tasks.Git.Operations.dll");
            var assembly = GitLoaderContext.Instance.LoadFromAssemblyPath(operationsPath);
#endif
            var type = assembly.GetType("Microsoft.Build.Tasks.Git.RepositoryTasks", throwOnError: true).GetTypeInfo();

            LocateRepository = (Func<LocateRepository, bool>)type.GetDeclaredMethod(nameof(LocateRepository)).CreateDelegate(typeof(Func<LocateRepository, bool>));
            GetRepositoryUrl = (Func<GetRepositoryUrl, bool>)type.GetDeclaredMethod(nameof(GetRepositoryUrl)).CreateDelegate(typeof(Func<GetRepositoryUrl, bool>));
            GetSourceRevisionId = (Func<GetSourceRevisionId, bool>)type.GetDeclaredMethod(nameof(GetSourceRevisionId)).CreateDelegate(typeof(Func<GetSourceRevisionId, bool>));
            GetSourceRoots = (Func<GetSourceRoots, bool>)type.GetDeclaredMethod(nameof(GetSourceRoots)).CreateDelegate(typeof(Func<GetSourceRoots, bool>));
            GetUntrackedFiles = (Func<GetUntrackedFiles, bool>)type.GetDeclaredMethod(nameof(GetUntrackedFiles)).CreateDelegate(typeof(Func<GetUntrackedFiles, bool>));
        }
    }
}
