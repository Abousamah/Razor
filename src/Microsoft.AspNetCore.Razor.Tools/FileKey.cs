// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Tools
{
    // From:
    // https://github.com/dotnet/roslyn/blob/749c0ec135d7d080658dc1aa794d15229c3d10d2/src/Compilers/Core/Portable/FileKey.cs
    internal struct FileKey : IEquatable<FileKey>
    {
        /// <summary>
        /// Full case-insensitive path.
        /// </summary>
        public readonly string FullPath;

        /// <summary>
        /// Last write time (UTC).
        /// </summary>
        public readonly DateTime Timestamp;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="fullPath">Full path.</param>
        /// <param name="timestamp">Last write time (UTC).</param>
        public FileKey(string fullPath, DateTime timestamp)
        {
            Debug.Assert(Path.IsPathRooted(fullPath));
            Debug.Assert(timestamp.Kind == DateTimeKind.Utc);

            FullPath = fullPath;
            Timestamp = timestamp;
        }

        /// <exception cref="IOException"/>
        public static FileKey Create(string fullPath)
        {
            return new FileKey(fullPath, GetFileTimeStamp(fullPath));
        }

        public override int GetHashCode()
        {
            var hashCodeCombiner = HashCodeCombiner.Start();
            hashCodeCombiner.Add(FullPath, StringComparer.OrdinalIgnoreCase);
            hashCodeCombiner.Add(Timestamp);

            return hashCodeCombiner.CombinedHash;
        }

        public override bool Equals(object obj)
        {
            return obj is FileKey && Equals((FileKey)obj);
        }

        public override string ToString()
        {
            return string.Format("'{0}'@{1}", FullPath, Timestamp);
        }

        public bool Equals(FileKey other)
        {
            return
                Timestamp == other.Timestamp &&
                string.Equals(FullPath, other.FullPath, StringComparison.OrdinalIgnoreCase);
        }

        internal static DateTime GetFileTimeStamp(string fullPath)
        {
            Debug.Assert(Path.IsPathRooted(fullPath));
            try
            {
                return File.GetLastWriteTimeUtc(fullPath);
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new IOException(e.Message, e);
            }
        }
    }
}
