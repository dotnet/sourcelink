// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.SourceLink.Tools
{
    internal sealed class Program
    {
        private static readonly Guid s_sourceLinkCustomDebugInformationId = new("CC110556-A091-4D38-9FEC-25AB9A351A6A");
        private static readonly Guid s_embeddedSourceCustomDebugInformationId = new("0E8A571B-6926-466E-B4AD-8AB04611F5FE");
        private static readonly byte[] s_crlfBytes = { (byte)'\r', (byte)'\n' };
        private static readonly ProductInfoHeaderValue s_sourceLinkProductHeaderValue = new("SourceLink", GetSourceLinkVersion());

        private static class AuthenticationMethod 
        {
            public const string Basic = "basic";
        }

        private record DocumentInfo(
            string ContainingFile,
            string Name,
            string? Uri,
            bool IsEmbedded,
            ImmutableArray<byte> Hash,
            Guid HashAlgorithm);

        private readonly ParseResult _parseResult;
        private bool _errorReported;

        public Program(ParseResult parseResult)
        {
            _parseResult = parseResult;
        }

        public static async Task<int> Main(string[] args)
        {
            var rootCommand = GetRootCommand();
            return await rootCommand.Parse(args).InvokeAsync();
        }

        private static string GetSourceLinkVersion()
        {
            var attribute = (AssemblyInformationalVersionAttribute)typeof(Program).Assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false).Single();
            return attribute.InformationalVersion.Split('+').First();
        }

        private static CliRootCommand GetRootCommand()
        {
            var pathArg = new CliArgument<string>("path")
            {
                Description = "Path to an assembly or .pdb"
            };
            var authArg = new CliOption<string>("--auth", "-a")
            {
                Description = "Authentication method"
            };
            authArg.AcceptOnlyFromAmong(AuthenticationMethod.Basic);

            var authEncodingArg = new CliOption<Encoding>("--auth-encoding", "-e")
            {
                CustomParser = arg => Encoding.GetEncoding(arg.Tokens.Single().Value),
                Description = "Encoding to use for authentication value"
            };

            var userArg = new CliOption<string>("--user", "-u")
            {
                Description = "Username to use to authenticate",
                Arity = ArgumentArity.ExactlyOne
            };

            var passwordArg = new CliOption<string>("--password", "-p")
            {
                Description = "Password to use to authenticate",
                Arity = ArgumentArity.ExactlyOne
            };

            var offlineArg = new CliOption<bool>("--offline")
            {
                Description = "Offline mode - skip validation of sourcelink URL targets"
            };

            var test = new CliCommand("test", "TODO")
            {
                pathArg,
                authArg,
                authEncodingArg,
                userArg,
                passwordArg,
                offlineArg,
            };

            test.SetAction((parseResult, cancellationToken) =>
            {
                string path = parseResult.GetValue(pathArg)!;
                string? authMethod = parseResult.GetValue(authArg);
                Encoding? authEncoding = parseResult.GetValue(authEncodingArg);
                string? user = parseResult.GetValue(userArg);
                string? password = parseResult.GetValue(passwordArg);
                bool offline = parseResult.GetValue(offlineArg);

                return TestAsync(path, authMethod, authEncoding, user, password, offline, parseResult, cancellationToken);
            });
            
            var printJson = new CliCommand("print-json", "Print Source Link JSON stored in the PDB")
            {
                pathArg
            };
            printJson.SetAction((parseResult, ct) => PrintJsonAsync(parseResult.GetValue(pathArg)!, parseResult));

            var printDocuments = new CliCommand("print-documents", "TODO")
            {
                pathArg
            };
            printDocuments.SetAction((parseResult, ct) => PrintDocumentsAsync(parseResult.GetValue(pathArg)!, parseResult));

            var printUrls = new CliCommand("print-urls", "TODO")
            {
                pathArg
            };
            printUrls.SetAction((parseResult, ct) => PrintUrlsAsync(parseResult.GetValue(pathArg)!, parseResult));

            var root = new CliRootCommand()
            {
                test,
                printJson,
                printDocuments,
                printUrls,
            };

            root.Description = "dotnet-sourcelink";

            root.Validators.Add(commandResult =>
            {
                if (commandResult.GetResult(authArg) != null)
                {
                    if (commandResult.GetResult(userArg) == null || commandResult.GetResult(passwordArg) == null)
                    {
                        commandResult.AddError("Specify --user and --password options");
                    }
                }
            });

            return root;
        }

        private void ReportError(string message)
        {
            _parseResult.Configuration.Error.Write(message);
            _parseResult.Configuration.Error.Write(Environment.NewLine);
            _errorReported = true;
        }

        private void WriteOutputLine(string message)
        {
            _parseResult.Configuration.Output.Write(message);
            _parseResult.Configuration.Output.Write(Environment.NewLine);
        }

        private static async Task<int> TestAsync(
            string path,
            string? authMethod,
            Encoding? authEncoding,
            string? user,
            string? password,
            bool offline,
            ParseResult parseResult,
            CancellationToken cancellationToken)
        {
            var authenticationHeader = (authMethod != null) ? GetAuthenticationHeader(authMethod, authEncoding ?? Encoding.ASCII, user!, password!) : null;

            try
            {
                return await new Program(parseResult).TestAsync(path, authenticationHeader, offline, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                parseResult.Configuration.Error.Write("Operation canceled.");
                parseResult.Configuration.Error.Write(Environment.NewLine);
                return -1;
            }
        }

        private async Task<int> TestAsync(string path, AuthenticationHeaderValue? authenticationHeader, bool offline, CancellationToken cancellationToken)
        {
            var documents = new List<DocumentInfo>();
            ReadAndResolveDocuments(path, documents);

            if (documents.Count == 0)
            {
                return _errorReported ? 1 : 0;
            }

            if (!offline)
            {
                using var handler = new HttpClientHandler();
                if (handler.SupportsAutomaticDecompression)
                    handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.UserAgent.Add(s_sourceLinkProductHeaderValue);
                client.DefaultRequestHeaders.Authorization = authenticationHeader;

                var outputLock = new object();

                var errorReporter = new Action<string>(message =>
                {
                    lock (outputLock)
                    {
                        ReportError(message);
                    }
                });

                var tasks = documents.Where(document => document.Uri != null).Select(document => DownloadAndValidateDocumentAsync(client, document, errorReporter, cancellationToken));

                _ = await Task.WhenAll(tasks).ConfigureAwait(false);

                if (_errorReported)
                {
                    return 1;
                }
            }

            WriteOutputLine($"File '{path}' validated.");
            return 0;
        }

        private static async Task<bool> DownloadAndValidateDocumentAsync(HttpClient client, DocumentInfo document, Action<string> reportError, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var request = new HttpRequestMessage(HttpMethod.Get, document.Uri);
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                reportError($"Failed to download '{document.Uri}': {response.ReasonPhrase} ({response.StatusCode})");
                return false;
            }

            var algorithmName = HashAlgorithmGuids.GetName(document.HashAlgorithm);

            // TODO: consider reusing buffers and IncrementalHash instances

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            // When git core.autocrfl option is true git replaces LF with CRLF on checkout, but only if the file has consistent line endings.
            // Line endings in files with mixed line endings are left unchanged.
            // The checksums stored in the PDB reflect the content of the checked out file on a build server,
            // hence they are calculated with the line endings changed.
            // First, check if the raw file checksum matches the PDB then check if file with LF converted to CRLF matches.

            cancellationToken.ThrowIfCancellationRequested();
            
            using var incrementalHash = IncrementalHash.CreateHash(algorithmName);
           
            incrementalHash.AppendData(content);
            var rawHash = incrementalHash.GetHashAndReset();
            if (document.Hash.SequenceEqual(rawHash))
            {
                return true;
            }

            cancellationToken.ThrowIfCancellationRequested();
            
            var crlfHash = TryCalculateHashWithLineBreakSubstituted(content, incrementalHash);
            if (crlfHash != null && document.Hash.SequenceEqual(crlfHash))
            {
                return true;
            }

            reportError($"Checksum validation failed for '{document.Uri}'.");
            return false;
        }

        private static byte[]? TryCalculateHashWithLineBreakSubstituted(byte[] content, IncrementalHash incrementalHash)
        {
            int index = 0;
            while (true)
            {
                int lf = Array.IndexOf(content, (byte)'\n', index);
                if (lf < 0)
                {
                    incrementalHash.AppendData(content, index, content.Length - index);
                    return incrementalHash.GetHashAndReset();
                }

                if (index - 1 >= 0 && content[index - 1] == (byte)'\r')
                {
                    // The file either has CRLF line endings or mixed line endings.
                    // In either case there is no need to substitute LF to CRLF.
                    _ = incrementalHash.GetHashAndReset();
                    return null;
                }

                incrementalHash.AppendData(content, index, lf - index);
                incrementalHash.AppendData(s_crlfBytes);
                index = lf + 1;
            }
        }

        private static Task<int> PrintJsonAsync(string path, ParseResult parseResult)
            => Task.FromResult(new Program(parseResult).PrintJson(path));

        private int PrintJson(string path)
        {
            ReadPdbMetadata(path, (filePath, metadataReader) =>
            {
                var sourceLink = ReadSourceLink(metadataReader);

                if (sourceLink == null)
                {
                    ReportError($"Source Link record not found in {filePath}.");
                }
                else
                {
                    WriteOutputLine(sourceLink);
                }
            });

            return _errorReported ? 1 : 0;
        }

        private static Task<int> PrintDocumentsAsync(string path, ParseResult parseResult)
            => Task.FromResult(new Program(parseResult).PrintDocuments(path));

        public static string ToHex(byte[] bytes)
            => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

        private int PrintDocuments(string path)
        {
            ReadPdbMetadata(path, (_, metadataReader) =>
            {
                foreach (var documentHandle in metadataReader.Documents)
                {
                    var document = metadataReader.GetDocument(documentHandle);
                    var hash = metadataReader.GetBlobBytes(document.Hash);
                    var hashAlgorithm = metadataReader.GetGuid(document.HashAlgorithm);
                    var language = metadataReader.GetGuid(document.Language);
                    var name = metadataReader.GetString(document.Name);

                    WriteOutputLine($"'{name}' {ToHex(hash)} {HashAlgorithmGuids.TryGetName(hashAlgorithm)?.Name ?? hashAlgorithm.ToString()} {LanguageGuids.GetName(language)}");
                }
            });

            return _errorReported ? 1 : 0;
        }

        private static Task<int> PrintUrlsAsync(string path, ParseResult parseResult)
            => Task.FromResult(new Program(parseResult).PrintUrls(path));

        private int PrintUrls(string path)
        {
            var resolvedDocuments = new List<DocumentInfo>();
            ReadAndResolveDocuments(path, resolvedDocuments);            

            int unresolvedCount = 0;
            foreach (var document in resolvedDocuments)
            {
                if (document.IsEmbedded)
                {
                    WriteOutputLine($"'{document.Name}': embedded");
                }
                else if (document.Uri != null)
                {
                    WriteOutputLine($"'{document.Name}': '{document.Uri}'");
                }
                else
                {
                    unresolvedCount++;
                }
            }

            if (unresolvedCount > 0)
            {
                ReportError($"Unable to resolve URL for {unresolvedCount} document(s):");
            }

            foreach (var document in resolvedDocuments)
            {
                if (!document.IsEmbedded && document.Uri == null)
                {
                    WriteOutputLine(document.Name);
                }
            }

            return _errorReported ? 1 : 0;
        }

        private bool ReadPdbMetadata(string path, Action<string, MetadataReader> reader)
        {
            var filePath = path;

            try
            {
                if (string.Equals(Path.GetExtension(path), ".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    using var provider = MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(path));
                    reader(filePath, provider.GetMetadataReader());
                    return true;
                }

                using var peReader = new PEReader(File.OpenRead(path));
                if (peReader.TryOpenAssociatedPortablePdb(path, pdbFileStreamProvider: File.OpenRead, out var pdbReaderProvider, out filePath))
                {
                    using (pdbReaderProvider)
                    {
                        reader(filePath ?? path, pdbReaderProvider!.GetMetadataReader());
                    }

                    return true;
                }

                return false;
            }
            catch (Exception e)
            {
                ReportError($"Error reading '{filePath}': {e.Message}");
                return false;
            }
        }

        private void ReadAndResolveDocuments(string path, List<DocumentInfo> resolvedDocuments)
        {
            if (!ReadPdbMetadata(path, (filePath, metadataReader) =>
            {
                var documents = new List<(string name, ImmutableArray<byte> hash, Guid hashAlgorithm, bool isEmbedded)>();
                bool hasUnembeddedDocument = false;

                foreach (var documentHandle in metadataReader.Documents)
                {
                    var document = metadataReader.GetDocument(documentHandle);
                    var name = metadataReader.GetString(document.Name);
                    var isEmbedded = HasCustomDebugInformation(metadataReader, documentHandle, s_embeddedSourceCustomDebugInformationId);
                    var hash = metadataReader.GetBlobContent(document.Hash);
                    var hashAlgorithm = metadataReader.GetGuid(document.HashAlgorithm);

                    documents.Add((name, hash, hashAlgorithm, isEmbedded));

                    if (!isEmbedded)
                    {
                        hasUnembeddedDocument = true;
                    }
                }

                SourceLinkMap sourceLinkMap = default;
                if (hasUnembeddedDocument)
                {
                    var sourceLink = ReadSourceLink(metadataReader);
                    if (sourceLink == null)
                    {
                        ReportError($"Source Link record not found.");
                        return;
                    }

                    try
                    {
                        sourceLinkMap = SourceLinkMap.Parse(sourceLink);
                    }
                    catch (Exception e)
                    {
                        ReportError($"Error reading SourceLink: {e.Message}");
                        return;
                    }
                }

                foreach (var (name, hash, hashAlgorithm, isEmbedded) in documents)
                {
                    string? uri = isEmbedded ? null : sourceLinkMap.TryGetUri(name, out var mappedUri) ? mappedUri : null;
                    resolvedDocuments.Add(new DocumentInfo(filePath, name, uri, isEmbedded, hash, hashAlgorithm));
                }
            }))
            {
                ReportError($"Symbol information not found for '{path}'.");
            };
        }

        private static bool HasCustomDebugInformation(MetadataReader metadataReader, EntityHandle handle, Guid kind)
        {
            foreach (var cdiHandle in metadataReader.GetCustomDebugInformation(handle))
            {
                var cdi = metadataReader.GetCustomDebugInformation(cdiHandle);
                if (metadataReader.GetGuid(cdi.Kind) == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static BlobReader GetCustomDebugInformationReader(MetadataReader metadataReader, EntityHandle handle, Guid kind)
        {
            foreach (var cdiHandle in metadataReader.GetCustomDebugInformation(handle))
            {
                var cdi = metadataReader.GetCustomDebugInformation(cdiHandle);
                if (metadataReader.GetGuid(cdi.Kind) == kind)
                {
                    return metadataReader.GetBlobReader(cdi.Value);
                }
            }

            return default;
        }

        private static string? ReadSourceLink(MetadataReader metadataReader)
        {
            var blobReader = GetCustomDebugInformationReader(metadataReader, EntityHandle.ModuleDefinition, s_sourceLinkCustomDebugInformationId);
            return blobReader.Length > 0 ? blobReader.ReadUTF8(blobReader.Length) : null;
        }

        private static AuthenticationHeaderValue GetAuthenticationHeader(string method, Encoding encoding, string username, string password)
        {
            return (method.ToLowerInvariant()) switch
            {
                AuthenticationMethod.Basic => new AuthenticationHeaderValue(
                    scheme: AuthenticationMethod.Basic,
                    parameter: Convert.ToBase64String(encoding.GetBytes($"{username}:{password}"))),

                _ => throw new InvalidOperationException(),
            };
        }
    }
}
