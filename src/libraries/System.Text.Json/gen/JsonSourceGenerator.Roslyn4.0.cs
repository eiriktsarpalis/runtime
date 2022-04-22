// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//#define LAUNCH_DEBUGGER
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.Json.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace System.Text.Json.SourceGeneration
{
    /// <summary>
    /// Generates source code to optimize serialization and deserialization with JsonSerializer.
    /// </summary>
    [Generator]
    public sealed partial class JsonSourceGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
#if LAUNCH_DEBUGGER
            if (!Diagnostics.Debugger.IsAttached)
            {
                Diagnostics.Debugger.Launch();
            }
#endif
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(static (s, _) => Parser.IsSyntaxTargetForGeneration(s), static (s, _) => Parser.GetSemanticTargetForGeneration(s))
                .Where(static c => c is not null);

            IncrementalValuesProvider<(ClassDeclarationSyntax Class, Compilation Compilation)> compilationAndClass = classDeclarations
                .Combine(context.CompilationProvider)
                .WithComparer(IgnoreCompilationEqualityComparer.Instance);

            context.RegisterSourceOutput(compilationAndClass, (spc, source) => Execute(source.Compilation, source.Class, spc));
        }

        private void Execute(Compilation compilation, ClassDeclarationSyntax contextClass, SourceProductionContext sourceProductionContext)
        {
            JsonSourceGenerationContext context = new JsonSourceGenerationContext(sourceProductionContext);
            Parser parser = new(compilation, context);
            SourceGenerationSpec? spec = parser.GetGenerationSpec(new[] { contextClass });
            if (spec != null)
            {
                _rootTypes = spec.ContextGenerationSpecList[0].RootSerializableTypes;

                Emitter emitter = new(context, spec);
                emitter.Emit();
            }
        }

        /// <summary>
        /// Helper for unit tests.
        /// </summary>
        public Dictionary<string, Type>? GetSerializableTypes() => _rootTypes?.ToDictionary(p => p.Type.FullName, p => p.Type);
        private List<TypeGenerationSpec>? _rootTypes;

        private class IgnoreCompilationEqualityComparer : IEqualityComparer<(ClassDeclarationSyntax Class, Compilation Compilation)>
        {
            public static IgnoreCompilationEqualityComparer Instance { get; } = new();

            public bool Equals((ClassDeclarationSyntax Class, Compilation Compilation) x, (ClassDeclarationSyntax Class, Compilation Compilation) y)
                => x.Class.IsEquivalentTo(y.Class);

            public int GetHashCode((ClassDeclarationSyntax Class, Compilation Compilation) obj) => obj.Class.GetHashCode();
        }
    }

    internal readonly struct JsonSourceGenerationContext
    {
        private readonly SourceProductionContext _context;

        public JsonSourceGenerationContext(SourceProductionContext context)
        {
            _context = context;
        }

        public void ReportDiagnostic(Diagnostic diagnostic)
        {
            _context.ReportDiagnostic(diagnostic);
        }

        public void AddSource(string hintName, SourceText sourceText)
        {
            _context.AddSource(hintName, sourceText);
        }
    }
}
