// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System;

namespace Microsoft.SourceLink.Common
{
    /// <summary>
    /// Used for serialization of the sourcelink files. Only supports the basic elements generated in this project.
    /// </summary>
    [DataContract]
    public class SourceLinks
    {
        [DataMember]
        public Dictionary<string, string> documents = new Dictionary<string, string>();
    }

    public sealed class MergeSourceLinkFiles : Task
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

        public override bool Execute()
        {
            try
            {
                var additionalSourceLinks = new List<SourceLinks>();

                // Read the original sourcelink file
                var sourceLink = ReadSourceLinkFile(SourceLinkFile);

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
                        additionalSourceLinks.Add(ReadSourceLinkFile(sourceLinkName));
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
                            additionalSourceLinks.Add(ReadSourceLinkFile(sourceLinkName));
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
                                additionalSourceLinks.Add(ReadSourceLinkFile(potentialPath));
                                break;
                            }
                        }
                    }
                }

                // Merge all the sourcelinks together and write back to the original sourcelink file path
                MergeSourceLinks(sourceLink, additionalSourceLinks);
                WriteSourceLinkFile(sourceLink, SourceLinkFile);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError("Failed to merge sourcelink files for libs with dll/exe sourcelink file - {0}", ex.Message);
            }

            return false;
        }

        private void MergeSourceLinks(SourceLinks sourceLink, List<SourceLinks> additionalSourceLinks)
        {
            foreach (var additionalSourceLink in additionalSourceLinks)
            {
                foreach (var document in additionalSourceLink.documents)
                {
                    if ( !sourceLink.documents.ContainsKey(document.Key))
                    {
                        sourceLink.documents.Add(document.Key, document.Value);
                        Log.LogMessage("Additional sourcelink document {0}: {1}", document.Key, document.Value);
                    }
                    else
                    {
                        Log.LogMessage("Sourcelink document {0} already exists", document.Key);
                    }
                }
            }
        }

        private static SourceLinks ReadSourceLinkFile(string path)
        {
            DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            };

            var sourceLink = new SourceLinks();
            using (var fileStream = File.OpenRead(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(SourceLinks), settings);
                return (SourceLinks)serializer.ReadObject(fileStream);
            }
        }

        private static void WriteSourceLinkFile(SourceLinks sourceLink, string path)
        {
            DataContractJsonSerializerSettings settings = new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            };

            using (var fileStream = File.OpenWrite(path))
            {
                var serializer = new DataContractJsonSerializer(typeof(SourceLinks), settings);
                serializer.WriteObject(fileStream, sourceLink);
            }
        }
    }
}