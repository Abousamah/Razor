﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.Language.Legacy;

namespace Microsoft.AspNetCore.Razor.Language.Extensions
{
    internal sealed class DefaultTagHelperTargetExtension : IDefaultTagHelperTargetExtension
    {
        private static readonly string[] PrivateModifiers = new string[] { "private" };

        public bool DesignTime { get; set; }

        public string RunnerVariableName { get; set; } = "__tagHelperRunner";

        public string StringValueBufferVariableName { get; set; } = "__tagHelperStringValueBuffer";

        public string CreateTagHelperMethodName { get; set; } = "CreateTagHelper";

        public string ExecutionContextTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.Runtime.TagHelpers.TagHelperExecutionContext";

        public string ExecutionContextVariableName { get; set; } = "__tagHelperExecutionContext";

        public string ExecutionContextAddMethodName { get; set; } = "Add";

        public string TagHelperRunnerTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.Runtime.TagHelpers.TagHelperRunner";

        public string ExecutionContextOutputPropertyName { get; set; } = "Output";

        public string ExecutionContextSetOutputContentAsyncMethodName { get; set; } = "SetOutputContentAsync";

        public string ExecutionContextAddHtmlAttributeMethodName { get; set; } = "AddHtmlAttribute";

        public string ExecutionContextAddTagHelperAttributeMethodName { get; set; } = "AddTagHelperAttribute";

        public string RunnerRunAsyncMethodName { get; set; } = "RunAsync";

        public string ScopeManagerTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.Runtime.TagHelpers.TagHelperScopeManager";

        public string ScopeManagerVariableName { get; set; } = "__tagHelperScopeManager";

        public string ScopeManagerBeginMethodName { get; set; } = "Begin";

        public string ScopeManagerEndMethodName { get; set; } = "End";

        public string StartTagHelperWritingScopeMethodName { get; set; } = "StartTagHelperWritingScope";

        public string EndTagHelperWritingScopeMethodName { get; set; } = "EndTagHelperWritingScope";

        public string TagModeTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.TagHelpers.TagMode";

        public string HtmlAttributeValueStyleTypeName { get; set; } = "global::Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeValueStyle";

        public string TagHelperOutputIsContentModifiedPropertyName { get; set; } = "IsContentModified";

        public string BeginAddHtmlAttributeValuesMethodName { get; set; } = "BeginAddHtmlAttributeValues";

        public string EndAddHtmlAttributeValuesMethodName { get; set; } = "EndAddHtmlAttributeValues";

        public string BeginWriteTagHelperAttributeMethodName { get; set; } = "BeginWriteTagHelperAttribute";

        public string EndWriteTagHelperAttributeMethodName { get; set; } = "EndWriteTagHelperAttribute";

        public string MarkAsHtmlEncodedMethodName { get; set; } = "Html.Raw";

        public string FormatInvalidIndexerAssignmentMethodName { get; set; } = "InvalidTagHelperIndexerAssignment";

        public string WriteTagHelperOutputMethod { get; set; } = "Write";

        public void WriteTagHelperBody(CodeRenderingContext context, DefaultTagHelperBodyIntermediateNode node)
        {
            if (DesignTime)
            {
                context.RenderChildren(node);
            }
            else
            {
                // Call into the tag helper scope manager to start a new tag helper scope.
                // Also capture the value as the current execution context.
                context.CodeWriter
                    .WriteStartAssignment(ExecutionContextVariableName)
                    .WriteStartInstanceMethodInvocation(
                        ScopeManagerVariableName,
                        ScopeManagerBeginMethodName);

                // Assign a unique ID for this instance of the source HTML tag. This must be unique
                // per call site, e.g. if the tag is on the view twice, there should be two IDs.
                var uniqueId = (string)context.Items[CodeRenderingContext.SuppressUniqueIds];
                if (uniqueId == null)
                {
                    uniqueId = Guid.NewGuid().ToString("N");
                }

                context.CodeWriter.WriteStringLiteral(context.TagHelperRenderingContext.TagName)
                    .WriteParameterSeparator()
                    .Write(TagModeTypeName)
                    .Write(".")
                    .Write(context.TagHelperRenderingContext.TagMode.ToString())
                    .WriteParameterSeparator()
                    .WriteStringLiteral(uniqueId)
                    .WriteParameterSeparator();

                // We remove and redirect writers so TagHelper authors can retrieve content.
                using (context.Push(new RuntimeNodeWriter()))
                using (context.Push(new RuntimeTagHelperWriter()))
                {
                    using (context.CodeWriter.BuildAsyncLambda())
                    {
                        context.RenderChildren(node);
                    }
                }

                context.CodeWriter.WriteEndMethodInvocation();
            }
        }

        public void WriteTagHelperCreate(CodeRenderingContext context, DefaultTagHelperCreateIntermediateNode node)
        {
            context.CodeWriter
                .WriteStartAssignment(node.Field)
                .Write(CreateTagHelperMethodName)
                .WriteLine("<global::" + node.Type + ">();");

            if (!DesignTime)
            {
                context.CodeWriter.WriteInstanceMethodInvocation(
                    ExecutionContextVariableName,
                    ExecutionContextAddMethodName,
                    node.Field);
            }
        }

        public void WriteTagHelperExecute(CodeRenderingContext context, DefaultTagHelperExecuteIntermediateNode node)
        {
            if (!DesignTime)
            {
                context.CodeWriter
                    .Write("await ")
                    .WriteStartInstanceMethodInvocation(
                        RunnerVariableName,
                        RunnerRunAsyncMethodName)
                    .Write(ExecutionContextVariableName)
                    .WriteEndMethodInvocation();

                var tagHelperOutputAccessor = $"{ExecutionContextVariableName}.{ExecutionContextOutputPropertyName}";

                context.CodeWriter
                    .Write("if (!")
                    .Write(tagHelperOutputAccessor)
                    .Write(".")
                    .Write(TagHelperOutputIsContentModifiedPropertyName)
                    .WriteLine(")");

                using (context.CodeWriter.BuildScope())
                {
                    context.CodeWriter
                        .Write("await ")
                        .WriteInstanceMethodInvocation(
                            ExecutionContextVariableName,
                            ExecutionContextSetOutputContentAsyncMethodName);
                }

                context.CodeWriter
                    .WriteStartMethodInvocation(WriteTagHelperOutputMethod)
                    .Write(tagHelperOutputAccessor)
                    .WriteEndMethodInvocation()
                    .WriteStartAssignment(ExecutionContextVariableName)
                    .WriteInstanceMethodInvocation(
                        ScopeManagerVariableName,
                        ScopeManagerEndMethodName);
            }
        }

        public void WriteTagHelperHtmlAttribute(CodeRenderingContext context, DefaultTagHelperHtmlAttributeIntermediateNode node)
        {
            if (DesignTime)
            {
                context.RenderChildren(node);
            }
            else
            {
                var attributeValueStyleParameter = $"{HtmlAttributeValueStyleTypeName}.{node.AttributeStructure}";
                var isConditionalAttributeValue = node.Children.Any(
                    child => child is CSharpExpressionAttributeValueIntermediateNode || child is CSharpCodeAttributeValueIntermediateNode);

                // All simple text and minimized attributes will be pre-allocated.
                if (isConditionalAttributeValue)
                {
                    // Dynamic attribute value should be run through the conditional attribute removal system. It's
                    // unbound and contains C#.

                    // TagHelper attribute rendering is buffered by default. We do not want to write to the current
                    // writer.
                    var valuePieceCount = node.Children.Count(
                        child =>
                            child is HtmlAttributeValueIntermediateNode ||
                            child is CSharpExpressionAttributeValueIntermediateNode ||
                            child is CSharpCodeAttributeValueIntermediateNode ||
                            child is ExtensionIntermediateNode);

                    context.CodeWriter
                        .WriteStartMethodInvocation(BeginAddHtmlAttributeValuesMethodName)
                        .Write(ExecutionContextVariableName)
                        .WriteParameterSeparator()
                        .WriteStringLiteral(node.AttributeName)
                        .WriteParameterSeparator()
                        .Write(valuePieceCount.ToString(CultureInfo.InvariantCulture))
                        .WriteParameterSeparator()
                        .Write(attributeValueStyleParameter)
                        .WriteEndMethodInvocation();

                    using (context.Push(new TagHelperHtmlAttributeRuntimeNodeWriter()))
                    {
                        context.RenderChildren(node);
                    }

                    context.CodeWriter
                        .WriteMethodInvocation(
                            EndAddHtmlAttributeValuesMethodName,
                            ExecutionContextVariableName);
                }
                else
                {
                    // This is a data-* attribute which includes C#. Do not perform the conditional attribute removal or
                    // other special cases used when IsDynamicAttributeValue(). But the attribute must still be buffered to
                    // determine its final value.

                    // Attribute value is not plain text, must be buffered to determine its final value.
                    context.CodeWriter.WriteMethodInvocation(BeginWriteTagHelperAttributeMethodName);

                    // We're building a writing scope around the provided chunks which captures everything written from the
                    // page. Therefore, we do not want to write to any other buffer since we're using the pages buffer to
                    // ensure we capture all content that's written, directly or indirectly.
                    using (context.Push(new RuntimeNodeWriter()))
                    using (context.Push(new RuntimeTagHelperWriter()))
                    {
                        context.RenderChildren(node);
                    }

                    context.CodeWriter
                        .WriteStartAssignment(StringValueBufferVariableName)
                        .WriteMethodInvocation(EndWriteTagHelperAttributeMethodName)
                        .WriteStartInstanceMethodInvocation(
                            ExecutionContextVariableName,
                            ExecutionContextAddHtmlAttributeMethodName)
                        .WriteStringLiteral(node.AttributeName)
                        .WriteParameterSeparator()
                        .WriteStartMethodInvocation(MarkAsHtmlEncodedMethodName)
                        .Write(StringValueBufferVariableName)
                        .WriteEndMethodInvocation(endLine: false)
                        .WriteParameterSeparator()
                        .Write(attributeValueStyleParameter)
                        .WriteEndMethodInvocation();
                }
            }
        }

        public void WriteTagHelperProperty(CodeRenderingContext context, DefaultTagHelperPropertyIntermediateNode node)
        {
            var tagHelperRenderingContext = context.TagHelperRenderingContext;
            var propertyName = node.BoundAttribute.GetPropertyName();
            var propertyValueAccessor = GetTagHelperPropertyAccessor(node.IsIndexerNameMatch, node.Field, node.AttributeName, node.BoundAttribute);

            if (!DesignTime)
            {
                // Ensure that the property we're trying to set has initialized its dictionary bound properties.
                if (node.IsIndexerNameMatch &&
                    tagHelperRenderingContext.VerifiedPropertyDictionaries.Add($"{node.TagHelper.GetTypeName()}.{propertyName}"))
                {
                    // Throw a reasonable Exception at runtime if the dictionary property is null.
                    context.CodeWriter
                        .Write("if (")
                        .Write(node.Field)
                        .Write(".")
                        .Write(propertyName)
                        .WriteLine(" == null)");
                    using (context.CodeWriter.BuildScope())
                    {
                        // System is in Host.NamespaceImports for all MVC scenarios. No need to generate FullName
                        // of InvalidOperationException type.
                        context.CodeWriter
                            .Write("throw ")
                            .WriteStartNewObject(nameof(InvalidOperationException))
                            .WriteStartMethodInvocation(FormatInvalidIndexerAssignmentMethodName)
                            .WriteStringLiteral(node.AttributeName)
                            .WriteParameterSeparator()
                            .WriteStringLiteral(node.TagHelper.GetTypeName())
                            .WriteParameterSeparator()
                            .WriteStringLiteral(propertyName)
                            .WriteEndMethodInvocation(endLine: false)   // End of method call
                            .WriteEndMethodInvocation();   // End of new expression / throw statement
                    }
                }
            }

            if (tagHelperRenderingContext.RenderedBoundAttributes.TryGetValue(node.AttributeName, out var previousValueAccessor))
            {
                context.CodeWriter
                    .WriteStartAssignment(propertyValueAccessor)
                    .Write(previousValueAccessor)
                    .WriteLine(";");

                return;
            }
            else
            {
                tagHelperRenderingContext.RenderedBoundAttributes[node.AttributeName] = propertyValueAccessor;
            }

            if (node.BoundAttribute.IsStringProperty || (node.IsIndexerNameMatch && node.BoundAttribute.IsIndexerStringProperty))
            {
                if (DesignTime)
                {
                    context.RenderChildren(node);

                    context.CodeWriter.WriteStartAssignment(propertyValueAccessor);
                    if (node.Children.Count == 1 && node.Children.First() is HtmlContentIntermediateNode htmlNode)
                    {
                        var content = GetContent(htmlNode);
                        context.CodeWriter.WriteStringLiteral(content);
                    }
                    else
                    {
                        context.CodeWriter.Write("string.Empty");
                    }
                    context.CodeWriter.WriteLine(";");
                }
                else
                {
                    context.CodeWriter.WriteMethodInvocation(BeginWriteTagHelperAttributeMethodName);

                    using (context.Push(new LiteralRuntimeNodeWriter()))
                    {
                        context.RenderChildren(node);
                    }

                    context.CodeWriter
                        .WriteStartAssignment(StringValueBufferVariableName)
                        .WriteMethodInvocation(EndWriteTagHelperAttributeMethodName)
                        .WriteStartAssignment(propertyValueAccessor)
                        .Write(StringValueBufferVariableName)
                        .WriteLine(";");
                }
            }
            else
            {
                if (DesignTime)
                {
                    var firstMappedChild = node.Children.FirstOrDefault(child => child.Source != null) as IntermediateNode;
                    var valueStart = firstMappedChild?.Source;

                    using (context.CodeWriter.BuildLinePragma(node.Source))
                    {
                        var assignmentPrefixLength = propertyValueAccessor.Length + " = ".Length;
                        if (node.BoundAttribute.IsEnum &&
                            node.Children.Count == 1 &&
                            node.Children.First() is IntermediateToken token &&
                            token.IsCSharp)
                        {
                            assignmentPrefixLength += $"global::{node.BoundAttribute.TypeName}.".Length;

                            if (valueStart != null)
                            {
                                context.CodeWriter.WritePadding(assignmentPrefixLength, node.Source, context);
                            }

                            context.CodeWriter
                                .WriteStartAssignment(propertyValueAccessor)
                                .Write("global::")
                                .Write(node.BoundAttribute.TypeName)
                                .Write(".");
                        }
                        else
                        {
                            if (valueStart != null)
                            {
                                context.CodeWriter.WritePadding(assignmentPrefixLength, node.Source, context);
                            }

                            context.CodeWriter.WriteStartAssignment(propertyValueAccessor);
                        }

                        RenderTagHelperAttributeInline(context, node, node.Source);

                        context.CodeWriter.WriteLine(";");
                    }
                }
                else
                {
                    using (context.CodeWriter.BuildLinePragma(node.Source))
                    {
                        context.CodeWriter.WriteStartAssignment(propertyValueAccessor);

                        if (node.BoundAttribute.IsEnum &&
                            node.Children.Count == 1 &&
                            node.Children.First() is IntermediateToken token &&
                            token.IsCSharp)
                        {
                            context.CodeWriter
                                .Write("global::")
                                .Write(node.BoundAttribute.TypeName)
                                .Write(".");
                        }

                        RenderTagHelperAttributeInline(context, node, node.Source);

                        context.CodeWriter.WriteLine(";");
                    }
                }
            }

            if (!DesignTime)
            {
                // We need to inform the context of the attribute value.
                context.CodeWriter
                    .WriteStartInstanceMethodInvocation(
                        ExecutionContextVariableName,
                        ExecutionContextAddTagHelperAttributeMethodName)
                    .WriteStringLiteral(node.AttributeName)
                    .WriteParameterSeparator()
                    .Write(propertyValueAccessor)
                    .WriteParameterSeparator()
                    .Write($"global::Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeValueStyle.{node.AttributeStructure}")
                    .WriteEndMethodInvocation();
            }
        }

        public void WriteTagHelperRuntime(CodeRenderingContext context, DefaultTagHelperRuntimeIntermediateNode node)
        {
            if (!DesignTime)
            {
                context.CodeWriter.WriteLine("#line hidden");

                // Need to disable the warning "X is never used." for the value buffer since
                // whether it's used depends on how a TagHelper is used.
                context.CodeWriter.WriteLine("#pragma warning disable 0169");
                context.CodeWriter.WriteField(PrivateModifiers, "string", StringValueBufferVariableName);
                context.CodeWriter.WriteLine("#pragma warning restore 0169");

                context.CodeWriter.WriteField(PrivateModifiers, ExecutionContextTypeName, ExecutionContextVariableName);

                context.CodeWriter
                    .Write("private ")
                    .Write(TagHelperRunnerTypeName)
                    .Write(" ")
                    .Write(RunnerVariableName)
                    .Write(" = new ")
                    .Write(TagHelperRunnerTypeName)
                    .WriteLine("();");

                var backedScopeManageVariableName = "__backed" + ScopeManagerVariableName;
                context.CodeWriter
                    .Write("private ")
                    .WriteVariableDeclaration(
                        ScopeManagerTypeName,
                        backedScopeManageVariableName,
                        value: null);

                context.CodeWriter
                .Write("private ")
                .Write(ScopeManagerTypeName)
                .Write(" ")
                .WriteLine(ScopeManagerVariableName);

                using (context.CodeWriter.BuildScope())
                {
                    context.CodeWriter.WriteLine("get");
                    using (context.CodeWriter.BuildScope())
                    {
                        context.CodeWriter
                            .Write("if (")
                            .Write(backedScopeManageVariableName)
                            .WriteLine(" == null)");

                        using (context.CodeWriter.BuildScope())
                        {
                            context.CodeWriter
                                .WriteStartAssignment(backedScopeManageVariableName)
                                .WriteStartNewObject(ScopeManagerTypeName)
                                .Write(StartTagHelperWritingScopeMethodName)
                                .WriteParameterSeparator()
                                .Write(EndTagHelperWritingScopeMethodName)
                                .WriteEndMethodInvocation();
                        }

                        context.CodeWriter
                            .Write("return ")
                            .Write(backedScopeManageVariableName)
                            .WriteLine(";");
                    }
                }
            }
        }

        private void RenderTagHelperAttributeInline(
            CodeRenderingContext context,
            DefaultTagHelperPropertyIntermediateNode property,
            SourceSpan? span)
        {
            for (var i = 0; i < property.Children.Count; i++)
            {
                RenderTagHelperAttributeInline(context, property, property.Children[i], span);
            }
        }

        private void RenderTagHelperAttributeInline(
            CodeRenderingContext context,
            DefaultTagHelperPropertyIntermediateNode property,
            IntermediateNode node,
            SourceSpan? span)
        {
            if (node is CSharpExpressionIntermediateNode || node is HtmlContentIntermediateNode)
            {
                for (var i = 0; i < node.Children.Count; i++)
                {
                    RenderTagHelperAttributeInline(context, property, node.Children[i], span);
                }
            }
            else if (node is IntermediateToken token)
            {
                if (DesignTime && node.Source != null)
                {
                    context.AddLineMappingFor(node);
                }

                context.CodeWriter.Write(token.Content);
            }
            else if (node is CSharpCodeIntermediateNode)
            {
                var error = new RazorError(
                    LegacyResources.TagHelpers_CodeBlocks_NotSupported_InAttributes,
                    SourceLocation.FromSpan(span),
                    span == null ? -1 : span.Value.Length);
                context.Diagnostics.Add(RazorDiagnostic.Create(error));
            }
            else if (node is TemplateIntermediateNode)
            {
                var expectedTypeName = property.IsIndexerNameMatch ? property.BoundAttribute.IndexerTypeName : property.BoundAttribute.TypeName;
                var error = new RazorError(
                    LegacyResources.FormatTagHelpers_InlineMarkupBlocks_NotSupported_InAttributes(expectedTypeName),
                    SourceLocation.FromSpan(span),
                    span == null ? -1 : span.Value.Length);
                context.Diagnostics.Add(RazorDiagnostic.Create(error));
            }
        }

        private string GetContent(HtmlContentIntermediateNode node)
        {
            var builder = new StringBuilder();
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (node.Children[i] is IntermediateToken token && token.IsHtml)
                {
                    builder.Append(token.Content);
                }
            }

            return builder.ToString();
        }

        private static string GetTagHelperPropertyAccessor(
            bool isIndexerNameMatch,
            string tagHelperVariableName,
            string attributeName,
            BoundAttributeDescriptor descriptor)
        {
            var propertyAccessor = $"{tagHelperVariableName}.{descriptor.GetPropertyName()}";

            if (isIndexerNameMatch)
            {
                var dictionaryKey = attributeName.Substring(descriptor.IndexerNamePrefix.Length);
                propertyAccessor += $"[\"{dictionaryKey}\"]";
            }

            return propertyAccessor;
        }
    }
}