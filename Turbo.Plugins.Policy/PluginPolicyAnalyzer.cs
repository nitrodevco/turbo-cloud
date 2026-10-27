using System;
using System.Collections.Immutable;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Turbo.Plugins.Policy;

/// <summary>
/// What a plugin's code may not do, as compile errors: load other code (assemblies, native
/// libraries, code generated at run time), use unsafe code, start processes or drive the compiler.
/// The server compiles every plugin from its sources and loads nothing else; these are the ways
/// around that, and the ways to read or write memory the runtime does not check.
/// It is a norm a plugin is held to, not a sandbox: a plugin runs with the server's own access
/// (reflection reaches anything), so a plugin is read before it is installed
/// (docs/plugin-loading.md).
/// Only the plugin's own files are read: code the server's pinned source generators add has no
/// path on disk, and is the host's.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginPolicyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Goes up when the rules change, so a plugin compiled under the old ones is compiled again.</summary>
    public const int VERSION = 1;

    private const string CATEGORY = "TurboPluginPolicy";

    public static readonly DiagnosticDescriptor LOADS_ASSEMBLIES = Rule(
        "TPP001",
        "Plugins may not load assemblies",
        "Plugins may not load assemblies ({0}): the server compiles a plugin from its own sources and loads nothing else"
    );

    public static readonly DiagnosticDescriptor NATIVE_CODE = Rule(
        "TPP002",
        "Plugins may not call native code",
        "Plugins may not call native code ({0})"
    );

    public static readonly DiagnosticDescriptor UNSAFE_CODE = Rule(
        "TPP003",
        "Plugins may not use unsafe code",
        "Plugins may not use unsafe code ({0}): it reads and writes memory the runtime does not check"
    );

    public static readonly DiagnosticDescriptor EMITS_CODE = Rule(
        "TPP004",
        "Plugins may not generate code at run time",
        "Plugins may not generate code at run time ({0}, System.Reflection.Emit)"
    );

    public static readonly DiagnosticDescriptor STARTS_PROCESSES = Rule(
        "TPP005",
        "Plugins may not start processes",
        "Plugins may not start processes ({0})"
    );

    public static readonly DiagnosticDescriptor USES_COMPILER = Rule(
        "TPP006",
        "Plugins may not use the compiler",
        "Plugins may not use the compiler ({0}, Microsoft.CodeAnalysis)"
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
    [LOADS_ASSEMBLIES, NATIVE_CODE, UNSAFE_CODE, EMITS_CODE, STARTS_PROCESSES, USES_COMPILER];

    public override void Initialize(AnalysisContext context)
    {
        // A file that calls itself generated is read all the same: only the plugin's files count.
        context.ConfigureGeneratedCodeAnalysis(
            GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics
        );
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(OnName, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        context.RegisterSyntaxNodeAction(
            OnUnsafeSyntax,
            SyntaxKind.UnsafeStatement,
            SyntaxKind.PointerType,
            SyntaxKind.FunctionPointerType
        );
        context.RegisterSyntaxTreeAction(OnModifiers);
    }

    private static DiagnosticDescriptor Rule(string id, string title, string message) =>
        new(id, title, message, CATEGORY, DiagnosticSeverity.Error, isEnabledByDefault: true);

    /// <summary>A file of the plugin's: generated code has no path on disk.</summary>
    private static bool IsPluginFile(SyntaxTree tree) => Path.IsPathRooted(tree.FilePath);

    private static void OnName(SyntaxNodeAnalysisContext context)
    {
        if (!IsPluginFile(context.Node.SyntaxTree))
            return;

        var node = (SimpleNameSyntax)context.Node;

        if (NamesTheCompiler(node))
        {
            Report(context, USES_COMPILER, node, "Microsoft.CodeAnalysis");

            return;
        }

        var info = context.SemanticModel.GetSymbolInfo(node, context.CancellationToken);
        var symbol =
            info.Symbol ?? (info.CandidateSymbols.Length > 0 ? info.CandidateSymbols[0] : null);

        if (symbol is null || Banned(symbol) is not { } banned)
            return;

        Report(context, banned.Rule, node, banned.Name);
    }

    /// <summary><c>Microsoft.CodeAnalysis</c> written out, resolved or not.</summary>
    private static bool NamesTheCompiler(SimpleNameSyntax node)
    {
        if (node.Identifier.ValueText != "CodeAnalysis")
            return false;

        var left = node.Parent switch
        {
            QualifiedNameSyntax q when q.Right == node => q.Left,
            MemberAccessExpressionSyntax m when m.Name == node => m.Expression,
            _ => null,
        };

        return left switch
        {
            IdentifierNameSyntax id => id.Identifier.ValueText == "Microsoft",
            AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText == "Microsoft",
            _ => false,
        };
    }

    /// <summary>The rule <paramref name="symbol"/> breaks and how to name it; null when none.</summary>
    private static (DiagnosticDescriptor Rule, string Name)? Banned(ISymbol symbol)
    {
        if (symbol is IAliasSymbol alias)
            symbol = alias.Target;

        if (InNamespace(symbol, "Microsoft.CodeAnalysis"))
            return (USES_COMPILER, symbol.ToDisplayString());

        var type = symbol as ITypeSymbol ?? symbol.ContainingType;

        if (type is null)
            return null;

        if (type is IPointerTypeSymbol or IFunctionPointerTypeSymbol)
            return (UNSAFE_CODE, type.ToDisplayString());

        var typeName = Name(type);

        if (InNamespace(type, "System.Reflection.Emit"))
            return (EMITS_CODE, typeName);

        if (DerivesFrom(type, "System.Runtime.Loader.AssemblyLoadContext"))
            return (LOADS_ASSEMBLIES, typeName);

        switch (typeName)
        {
            case "System.Runtime.InteropServices.DllImportAttribute":
            case "System.Runtime.InteropServices.LibraryImportAttribute":
            case "System.Runtime.InteropServices.NativeLibrary":
                return (NATIVE_CODE, typeName);
            case "System.Runtime.CompilerServices.Unsafe":
                return (UNSAFE_CODE, typeName);
        }

        if (symbol is not IMethodSymbol method)
            return null;

        var call = $"{typeName}.{method.Name}";

        switch (typeName)
        {
            case "System.Reflection.Assembly"
                when method.Name.StartsWith("Load", StringComparison.Ordinal)
                    || method.Name.StartsWith("ReflectionOnlyLoad", StringComparison.Ordinal)
                    || method.Name == "UnsafeLoadFrom":
            case "System.AppDomain"
                when method.Name == "Load"
                    || method.Name.StartsWith("ExecuteAssembly", StringComparison.Ordinal):
            case "System.Activator"
                when method.Name.StartsWith("CreateInstanceFrom", StringComparison.Ordinal):
                return (LOADS_ASSEMBLIES, call);
            case "System.Diagnostics.Process" when method.Name == "Start":
                return (STARTS_PROCESSES, call);
            default:
                return null;
        }
    }

    private static void OnUnsafeSyntax(SyntaxNodeAnalysisContext context)
    {
        if (!IsPluginFile(context.Node.SyntaxTree))
            return;

        var what = context.Node switch
        {
            UnsafeStatementSyntax => "an unsafe block",
            FunctionPointerTypeSyntax => "a function pointer",
            _ => "a pointer",
        };

        Report(context, UNSAFE_CODE, context.Node, what);
    }

    /// <summary>The <c>unsafe</c> and <c>extern</c> modifiers, wherever they are written.</summary>
    private static void OnModifiers(SyntaxTreeAnalysisContext context)
    {
        if (!IsPluginFile(context.Tree))
            return;

        foreach (
            var token in context
                .Tree.GetRoot(context.CancellationToken)
                .DescendantTokens(descendIntoTrivia: false)
        )
        {
            if (token.IsKind(SyntaxKind.UnsafeKeyword) && token.Parent is not UnsafeStatementSyntax)
                context.ReportDiagnostic(
                    Diagnostic.Create(UNSAFE_CODE, token.GetLocation(), "the unsafe modifier")
                );
            else if (
                token.IsKind(SyntaxKind.ExternKeyword)
                && token.Parent is MemberDeclarationSyntax or LocalFunctionStatementSyntax
            )
                context.ReportDiagnostic(
                    Diagnostic.Create(NATIVE_CODE, token.GetLocation(), "an extern method")
                );
        }
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor rule,
        SyntaxNode node,
        string name
    ) => context.ReportDiagnostic(Diagnostic.Create(rule, node.GetLocation(), name));

    // Namespace and containing types, no type arguments: System.Reflection.Emit.ILGenerator.
    private static readonly SymbolDisplayFormat FULL_NAME = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces
    );

    private static string Name(ITypeSymbol type) =>
        type.OriginalDefinition.ToDisplayString(FULL_NAME);

    private static bool InNamespace(ISymbol symbol, string ns)
    {
        var name = (symbol as INamespaceSymbol ?? symbol.ContainingNamespace)?.ToDisplayString();

        return name is not null
            && (name == ns || name.StartsWith(ns + ".", StringComparison.Ordinal));
    }

    private static bool DerivesFrom(ITypeSymbol type, string baseName)
    {
        for (var t = type; t is not null; t = t.BaseType)
            if (Name(t) == baseName)
                return true;

        return false;
    }
}
