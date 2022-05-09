using System.Globalization;
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
        var accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
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

    public static string[] GetCssClassFromHtml(string value)
    {
        // todo, make this configurable.
        const string RegExPattern = @"(class\s*=\s*[\'\""](?<value>[^<]*?)[\'\""])|((cssclass\s*=\s*[\'\""](?<value>[^<]*?)[\'\""]))|(CssClass\s*\(\s*\""(?<value>[^<]*?)\""\s*\))";
        var matches = Regex.Matches(value, RegExPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var results = new string[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            results[i] = matches[i].Groups["value"].Captures[0].Value;
        }

        #if DEBUG
        var list = new List<string>(results) { DateTime.Now.ToString(CultureInfo.InvariantCulture) };
        results = list.ToArray();
        #endif

        return results;
    }

    public static string GetGeneratedFileName(string path)
    {
        return $"monorail-css-file-parser-{Path.GetFileName(path)}-{GetDeterministicHashCode(path)}-jit.g.cs";
    }

    public static string GetGeneratedMethodName(string path)
    {
        return $"Get{Path.GetFileName(path)}_{GetDeterministicHashCode(path)}".Replace(".", "_").Replace("-", "_");
    }

    static int GetDeterministicHashCode(this string str)
    {
        unchecked
        {
            int hash1 = (5381 << 16) + 5381;
            int hash2 = hash1;

            for (int i = 0; i < str.Length; i += 2)
            {
                hash1 = ((hash1 << 5) + hash1) ^ str[i];
                if (i == str.Length - 1)
                    break;
                hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
            }

            return hash1 + (hash2 * 1566083941);
        }
    }
}