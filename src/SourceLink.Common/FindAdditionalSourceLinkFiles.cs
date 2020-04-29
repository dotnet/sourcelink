// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;

namespace Microsoft.SourceLink.Common
{
    public sealed class FindAdditionalSourceLinkFiles : Task
    {
        /// <summary>
        /// The name/path of the sourcelink file that we will merge into.
        /// </summary>
        [Required, NotNull]
        public string? SourceLinkFile { get; set; }

        /// <summary>
        /// Collection of all the library directories that will be searched for lib files.
        /// </summary>
        [Required, NotNull]
        public string[]? AdditionalLibraryDirectories { get; set; }

        /// <summary>
        /// Collection of all the libs that we will link to.
        /// </summary>
        [Required, NotNull]
        public string[]? AdditionalDependencies { get; set; }

        /// <summary>
        /// Collection of solution referenced import libraries.
        /// </summary>
        [Required, NotNull]
        public string[]? ImportLibraries { get; set; }

        [Output]
        public string[]? AllSourceLinkFiles { get; set; }

        public override bool Execute()
        {
            List<string> allSourceLinkFiles = new List<string>();
            allSourceLinkFiles.Add(SourceLinkFile);

            try
            {
                //// Throughout we expect that the sourcelink files for a lib is alongside
                //// the lib with the extension sourcelink.json instead of lib.

                // For import libraries we always have the full path to the lib. This shouldn't be needed since
                // the path will be common to the dll/exe project. We have this in case there are out of tree
                // references to library projects.
                foreach (var importLib in ImportLibraries)
                {
                    string sourceLinkName = Path.ChangeExtension(importLib, "sourcelink.json");
                    if (File.Exists(sourceLinkName))
                    {
                        Log.LogMessage("Found additional sourcelink file '{0}'", sourceLinkName);
                        allSourceLinkFiles.Add(sourceLinkName);
                    }
                }

                // Try and find sourcelink files for each lib
                foreach (var dependency in AdditionalDependencies)
                {
                    string sourceLinkName = Path.ChangeExtension(dependency, "sourcelink.json");
                    if (Path.IsPathRooted(dependency))
                    {
                        // If the lib path is rooted just look for the sourcelink file with the appropriate extension
                        // on that path.
                        if (File.Exists(sourceLinkName))
                        {
                            Log.LogMessage("Found additional sourcelink file '{0}'", sourceLinkName);
                            allSourceLinkFiles.Add(sourceLinkName);
                        }
                    }
                    else
                    {
                        // Not-rooted, perform link like scanning of the lib directories to find the full lib path
                        // and then look for the sourcelink file alongside the lib with the appropriate extension.
                        foreach (var libDir in AdditionalLibraryDirectories)
                        {
                            string potentialPath = Path.Combine(libDir, sourceLinkName);
                            if (File.Exists(potentialPath))
                            {
                                Log.LogMessage("Found additional sourcelink file '{0}'", potentialPath);
                                allSourceLinkFiles.Add(potentialPath);
                                break;
                            }
                        }
                    }
                }

                AllSourceLinkFiles = allSourceLinkFiles.ToArray();
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to find sourcelink files for libs with dll/exe sourcelink file - {0}", ex.Message);
            }

            return false;
        }
    }
}