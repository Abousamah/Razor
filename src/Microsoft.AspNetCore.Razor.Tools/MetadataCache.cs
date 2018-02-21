// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Internal;

namespace Microsoft.AspNetCore.Razor.Tools
{
    internal class MetadataAndSymbolCache
    {
        // Store 100 entries -- arbitrary number
        private const int CacheSize = 100;
        private readonly ConcurrentLruCache<FileKey, Metadata> _metadataCache =
            new ConcurrentLruCache<FileKey, Metadata>(CacheSize);

        private ModuleMetadata CreateModuleMetadata(string path, bool prefetchEntireImage)
        {
            // TODO: exception handling?
            var fileStream = File.OpenRead(path);

            var options = PEStreamOptions.PrefetchMetadata;
            if (prefetchEntireImage)
            {
                options |= PEStreamOptions.PrefetchEntireImage;
            }

            return ModuleMetadata.CreateFromStream(fileStream, options);
        }

        private ImmutableArray<ModuleMetadata> GetAllModules(ModuleMetadata manifestModule, string assemblyDir)
        {
            List<ModuleMetadata> moduleBuilder = null;

            foreach (var moduleName in manifestModule.GetModuleNames())
            {
                if (moduleBuilder == null)
                {
                    moduleBuilder = new List<ModuleMetadata>();
                    moduleBuilder.Add(manifestModule);
                }

                var module = CreateModuleMetadata(Path.Combine(assemblyDir, moduleName), prefetchEntireImage: false);
                moduleBuilder.Add(module);
            }

            return (moduleBuilder != null) ? moduleBuilder.ToImmutableArray() : ImmutableArray.Create(manifestModule);
        }

        internal Metadata GetMetadata(string fullPath, MetadataReferenceProperties properties)
        {
            // Check if we have an entry in the dictionary.
            FileKey? fileKey = GetUniqueFileKey(fullPath);

            Metadata metadata;
            if (fileKey.HasValue && _metadataCache.TryGetValue(fileKey.Value, out metadata) && metadata != null)
            {
                return metadata;
            }

            if (properties.Kind == MetadataImageKind.Module)
            {
                var result = CreateModuleMetadata(fullPath, prefetchEntireImage: true);
                //?? never add modules to cache?
                return result;
            }
            else
            {
                var primaryModule = CreateModuleMetadata(fullPath, prefetchEntireImage: false);

                // Get all the modules, and load them. Create an assembly metadata.
                var allModules = GetAllModules(primaryModule, Path.GetDirectoryName(fullPath));
                Metadata result = AssemblyMetadata.Create(allModules);

                result = _metadataCache.GetOrAdd(fileKey.Value, result);

                return result;
            }
        }

        /// <summary>
        /// A unique file key encapsulates a file path, and change date
        /// that can be used as the key to a dictionary.
        /// If a file hasn't changed name or timestamp, we assume
        /// it is unchanged.
        /// 
        /// Returns null if the file doesn't exist or otherwise can't be accessed.
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

    internal sealed class CachingMetadataReference : PortableExecutableReference
    {
        private static readonly MetadataAndSymbolCache s_mdCache = new MetadataAndSymbolCache();

        public CachingMetadataReference(string fullPath, MetadataReferenceProperties properties)
            : base(properties, fullPath)
        {
        }

        protected override DocumentationProvider CreateDocumentationProvider()
        {
            return DocumentationProvider.Default;
        }

        protected override Metadata GetMetadataImpl()
        {
            return s_mdCache.GetMetadata(FilePath, Properties);
        }

        protected override PortableExecutableReference WithPropertiesImpl(MetadataReferenceProperties properties)
        {
            return new CachingMetadataReference(FilePath, properties);
        }
    }

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
            //Debug.Assert(PathUtilities.IsAbsolute(fullPath));
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
            //Debug.Assert(PathUtilities.IsAbsolute(fullPath));
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
