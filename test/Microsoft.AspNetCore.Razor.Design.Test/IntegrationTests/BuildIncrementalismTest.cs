﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.AspNetCore.Razor.Design.IntegrationTests
{
    public class BuildIncrementalismTest : MSBuildIntegrationTestBase
    {
        [Fact]
        [InitializeTestProject("SimpleMvc")]
        public async Task BuildIncremental_SimpleMvc_PersistsTargetInputFile()
        {
            // Arrange
            var thumbprintLookup = new Dictionary<string, FileThumbPrint>();

            // Act 1
            var result = await DotnetMSBuild("Build", $"/p:RazorCompileOnBuild=true");

            var directoryPath = Path.Combine(result.Project.DirectoryPath, IntermediateOutputPath);
            var filesToIgnore = new[]
            {
                // These files are generated on every build.
                Path.Combine(directoryPath, "SimpleMvc.csproj.CopyComplete"),
                Path.Combine(directoryPath, "SimpleMvc.csproj.FileListAbsolute.txt"),
            };
            var files = Directory.GetFiles(directoryPath).Where(p => !filesToIgnore.Contains(p));
            foreach (var file in files)
            {
                var thumbprint = GetThumbPrint(file);
                thumbprintLookup[file] = thumbprint;
            }

            // Assert 1
            Assert.BuildPassed(result);

            // Act & Assert 2
            for (var i = 0; i < 2; i++)
            {
                // We want to make sure nothing changed between multiple incremental builds.
                using (var razorGenDirectoryLock = LockDirectory(RazorIntermediateOutputPath))
                {
                    result = await DotnetMSBuild("Build", $"/p:RazorCompileOnBuild=true");
                }

                Assert.BuildPassed(result);
                foreach (var file in files)
                {
                    var thumbprint = GetThumbPrint(file);
                    Assert.Equal(thumbprintLookup[file], thumbprint);
                }
            }
        }
    }
}
