// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.CodeAnalysis;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal class AssemblyReferenceCache
    {
        // Store 1000 entries -- arbitrary number
        private const int CacheSize = 1000;
        private readonly ConcurrentLruCache<FileKey, PortableExecutableReference> _referenceCache =
                new ConcurrentLruCache<FileKey, PortableExecutableReference>(CacheSize);

        internal PortableExecutableReference GetAssemblyReference(string fullPath)
        {
            // Check if we have an entry in the dictionary.
            var fileKey = GetUniqueFileKey(fullPath);

            if (fileKey.HasValue &&
                _referenceCache.TryGetValue(fileKey.Value, out var assemblyReference) &&
                assemblyReference != null)
            {
                return assemblyReference;
            }

            var reference = MetadataReference.CreateFromFile(fullPath);
            reference = _referenceCache.GetOrAdd(fileKey.Value, reference);

            return reference;
        }

        /// <summary>
        /// A unique file key encapsulates a file path, and change date
        /// that can be used as the key to a dictionary.
        /// If a file hasn't changed name or timestamp, we assume
        /// it is unchanged.
        /// 
        /// Returns null if the file doesn't exist or otherwise can't be accessed.
        /// From:
        /// https://github.com/dotnet/roslyn/blob/749c0ec135d7d080658dc1aa794d15229c3d10d2/src/Compilers/Server/ServerShared/MetadataCache.cs#L93
        /// </summary>
        private FileKey? GetUniqueFileKey(string filePath)
        {
            try
            {
                return FileKey.Create(filePath);
            }
            catch (Exception)
            {
                // There are several exceptions that can occur here: NotSupportedException or PathTooLongException
                // for a bad path, UnauthorizedAccessException for access denied, etc. Rather than listing them all,
                // just catch all exceptions.
                return null;
            }
        }
    }
}
