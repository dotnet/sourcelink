// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the License.txt file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TestUtilities
{
    public sealed class TempRoot : IDisposable
    {
        private readonly ConcurrentBag<IDisposable> _temps = new ConcurrentBag<IDisposable>();
        public static readonly string Root;
        private bool _disposed;

        static TempRoot()
        {
            var root = Path.Combine(Path.GetTempPath(), "SourceLinkTests");

            Directory.CreateDirectory(root);

            // On OSX /tmp is a symlink to /private/tmp and libgit2 expands the symlink.
            // Path.GetFullPath doesn't expand symlinks. See also https://github.com/dotnet/corefx/issues/26310.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && Directory.Exists("/private" + root))
            {
                root = "/private" + root;
            }

            Root = root;
        }

        public void Dispose()
        {
            _disposed = true;
            while (_temps.TryTake(out var temp))
            {
                try
                {
                    if (temp != null)
                    {
                        temp.Dispose();
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        private void CheckDisposed()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(nameof(TempRoot));
            }
        }

        public TempDirectory CreateDirectory()
        {
            CheckDisposed();
            var dir = new DisposableDirectory(this);
            _temps.Add(dir);
            return dir;
        }

        public TempFile CreateFile(string? prefix = null, string? extension = null, string? directory = null, [CallerFilePath]string? callerSourcePath = null, [CallerLineNumber]int callerLineNumber = 0)
        {
            CheckDisposed();
            return AddFile(new DisposableFile(prefix, extension, directory, callerSourcePath, callerLineNumber));
        }

        public DisposableFile AddFile(DisposableFile file)
        {
            CheckDisposed();
            _temps.Add(file);
            return file;
        }

        internal static void CreateStream(string fullPath)
        {
            using (var file = new FileStream(fullPath, FileMode.CreateNew)) { }
        }
    }
}
