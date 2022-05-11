using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

internal static class Helpers
{
    public static INamedTypeSymbol? GetMonorailsSemanticTargetForGeneration(GeneratorSyntaxContext ctx)
    {
        // we know the node is a EnumDeclarationSyntax thanks to IsSyntaxTargetForGeneration
        var classDeclarationSyntax = (ClassDeclarationSyntax)ctx.Node;

        return ctx.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) ?? null;
    }

    public static bool IsMonorailClassSyntaxTargetForGeneration(SyntaxNode syntaxNode)
    {
        return syntaxNode is ClassDeclarationSyntax c
               && c.Modifiers.Any(i => i.IsKind(SyntaxKind.PartialKeyword))
               && c.Identifier.ToString().Equals("MonorailCSS", StringComparison.InvariantCultureIgnoreCase);
    }

    public static string GenerateExtensionClass(INamedTypeSymbol symbol, string methodName,
        HashSet<string> classesToGenerate)
    {
        var ns = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        var accessibility = GetAccessibility(symbol);
        var isStatic = symbol.IsStatic;

        var isFirst = true;
        var sb = new StringBuilder();
        if (ns == "<global namespace>")
        {
            ns = "Root";
        }

        sb.Append($@"namespace {ns}
{{
    {accessibility} {(isStatic ? "static " : "")}partial class {className}
    {{
        private static string[] {methodName}() => new string[] {{
");
        foreach (var css in classesToGenerate)
        {
            if (isFirst)
            {
                sb.Append($"\"{css}\"");
                isFirst = false;
            }
            else
            {
                sb.Append($", \"{css}\"");
            }
        }

        sb.Append(@"
        };
    }
}");

        return sb.ToString();
    }

    public static string[] GetCssClassFromHtml(string value, string regex)
    {
        var matches = Regex.Matches(value, regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var results = new string[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            results[i] = matches[i].Groups["value"].Captures[0].Value;
        }

        return results;
    }

    public static string GetAccessibility(INamedTypeSymbol symbol)
    {
        var accessibility = symbol.DeclaredAccessibility switch
        {
            Accessibility.NotApplicable => string.Empty,
            Accessibility.Private => "private",
            Accessibility.ProtectedAndInternal => "protected internal",
            Accessibility.Protected => "protected",
            Accessibility.Internal => "internal",
            Accessibility.ProtectedOrInternal => "internal",
            Accessibility.Public => "public",
            _ => throw new ArgumentOutOfRangeException()
        };
        return accessibility;
    }
}