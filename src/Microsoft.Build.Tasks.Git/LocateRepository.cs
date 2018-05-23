// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks.Git
{
    public class LocateRepository : Task
    {
        [Required]
        public string Directory { get; set; }

        [Output]
        public string Id { get; set; }

        public override bool Execute()
        {
            try
            {
                return TaskImplementation.LocateRepository(this);
            }
            catch (FileLoadException) 
            {
#if NET461
                foreach (var message in TaskImplementation.GetLog())
                {
                    Log.LogMessage(message);
                }
#endif
                throw;
            }
        }
    }
}
