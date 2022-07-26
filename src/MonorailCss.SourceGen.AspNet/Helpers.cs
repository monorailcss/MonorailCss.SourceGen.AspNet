using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

internal struct MonorailClassDefinition
{
    public MonorailClassDefinition(string ns, string classname, string modifiers)
    {
        Namespace = ns;
        Classname = classname;
        Modifiers = modifiers;
    }

    public string Namespace { get; }
    public string Classname { get; }
    public string Modifiers { get; }
}

internal static class Helpers
{
    public static MonorailClassDefinition GetMonorailsSemanticTargetForGeneration(GeneratorSyntaxContext ctx)
    {
        var classDeclarationSyntax = (ClassDeclarationSyntax)ctx.Node;

        var ns = GetNamespace(classDeclarationSyntax);
        var classname = classDeclarationSyntax.Identifier.Text;
        var modifiers = classDeclarationSyntax.Modifiers.ToString();

        return new MonorailClassDefinition(ns, classname, modifiers);
    }

    public static bool IsMonorailClassSyntaxTargetForGeneration(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not ClassDeclarationSyntax c) return false;

        var isPartial = false;

        // this is an extremely frequently called path, so avoid linq. by checking for
        // the partial keyword we should be able to exit pretty quickly without needing to compare strings.
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (var i in c.Modifiers)
        {
            if (i.IsKind(SyntaxKind.PartialKeyword))
            {
                isPartial = true;
                break;
            }
        }

        return isPartial && c.Identifier.ToString().Equals("MonorailCSS", StringComparison.InvariantCultureIgnoreCase);
    }

    public static string GenerateExtensionClass(
        MonorailClassDefinition symbol,
        string methodName,
        ImmutableHashSet<string> classesToGenerate)
    {
        var ns = symbol.Namespace;
        var className = symbol.Classname;
        var modifiers = symbol.Modifiers;

        if (ns == "<global namespace>")
        {
            ns = "Root";
        }

        if (classesToGenerate.Count == 0)
        {
            return $$"""
using System.Lists.Generic;

namespace {{ns}}
{
    {{modifiers}} class {{className}}
    {
        private static string[] {{methodName}}() => Array.Empty<string>();
    }
}
""";
        }

        return $$"""
namespace {{ns}}
{
    {{modifiers}} class {{className}}
    {
        private static string[] {{methodName}}() => new string[] {
            {{ string.Join(", ", classesToGenerate.Select(i => $"\"{i}\"")) }}
        };
    }
}
""";
    }

    public static string[] GetCssClassFromHtml(string value, string regex)
    {
        // without RegexOptions.Compiled this runs in about 40us vs 20us compiled, so we'd need about 1000
        // razor files for this to catch up in perf by compiling it.
        var matches = Regex.Matches(value, regex, RegexOptions.IgnoreCase | RegexOptions.Multiline);
        var results = new string[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            results[i] = matches[i].Groups["value"].Captures[0].Value;
        }

        return results;
    }

    // determine the namespace the class/enum/struct is declared in, if any
    static string GetNamespace(BaseTypeDeclarationSyntax syntax)
    {
        // If we don't have a namespace at all we'll return an empty string
        // This accounts for the "default namespace" case
        var nameSpace = string.Empty;

        // Get the containing syntax node for the type declaration
        // (could be a nested type, for example)
        var potentialNamespaceParent = syntax.Parent;

        // Keep moving "out" of nested classes etc until we get to a namespace
        // or until we run out of parents
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        // Build up the final namespace by looping until we no longer have a namespace declaration
        if (potentialNamespaceParent is not BaseNamespaceDeclarationSyntax namespaceParent)
        {
            return nameSpace;
        }

        // We have a namespace. Use that as the type
        nameSpace = namespaceParent.Name.ToString();

        // Keep moving "out" of the namespace declarations until we
        // run out of nested namespace declarations
        while (true)
        {
            if (namespaceParent.Parent is not NamespaceDeclarationSyntax parent)
            {
                break;
            }

            // Add the outer namespace as a prefix to the final namespace
            nameSpace = $"{namespaceParent.Name}.{nameSpace}";
            namespaceParent = parent;
        }

        // return the final namespace
        return nameSpace;
    }
}