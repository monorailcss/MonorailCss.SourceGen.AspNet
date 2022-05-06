using System.Collections.Immutable;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class CssJitGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for enums
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) =>
                    GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        // is this a call for AddAttribute where the attribute is named "class" or "cssclass"?
        IncrementalValuesProvider<string[]> addClassAttribute = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForAttributeClassGeneration(s),
                transform: static (ctx, _) => GetAttributeClassMethodCall(ctx))
            .Where(static m => m is not null)!;

        // or maybe we are calling a custom CssClass method that has a single parameter that is a string literal.
        IncrementalValuesProvider<string[]> addCssClassCall = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForCssClassCallGeneration(s),
                transform: static (ctx, _) => GetCssClassMethodMethodCall(ctx))
            .Where(static m => m is not null)!;

        // and finally if there is a call to add Markup then we need to parse that whole string looking for css classes
        IncrementalValuesProvider<string[]> addMarkup = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForAddMarkupGeneration(s),
                transform: static (ctx, _) => GetCssClassesFromAddMarkup(ctx))
            .Where(static m => m is not null)!;

        var allAddClassAttributes =
            monorailClassDeclarations.Combine(
                addClassAttribute.Collect().Combine(addMarkup.Collect())).Combine(addCssClassCall.Collect());

        context.RegisterSourceOutput(allAddClassAttributes, static (spc, source) => Execute(source, spc));
    }

    private static string[]? GetCssClassMethodMethodCall(GeneratorSyntaxContext ctx)
    {
        var invocationExpressionSyntax = (InvocationExpressionSyntax)ctx.Node;
        var argumentListArguments = invocationExpressionSyntax.ArgumentList.Arguments;

        return argumentListArguments[0].Expression is LiteralExpressionSyntax literalExpressionSyntax
            ? new[] { literalExpressionSyntax.ToString().Replace("\"", string.Empty) }
            : default;
    }

    private static bool IsSyntaxTargetForCssClassCallGeneration(SyntaxNode syntaxNode)
    {
        return syntaxNode is InvocationExpressionSyntax i
               && i.ArgumentList.Arguments.Count == 1
               && i.ArgumentList.Arguments[0].Expression is LiteralExpressionSyntax
               && (
                   i.Expression.ToString().Equals("CssClass", StringComparison.Ordinal)
                   || (i.Expression is MemberAccessExpressionSyntax me &&
                       me.Name.ToString().Equals("CssClass", StringComparison.Ordinal))
               );
    }

    private static INamedTypeSymbol? GetMonorailsSemanticTargetForGeneration(GeneratorSyntaxContext ctx)
    {
        // we know the node is a EnumDeclarationSyntax thanks to IsSyntaxTargetForGeneration
        var classDeclarationSyntax = (ClassDeclarationSyntax)ctx.Node;

        return ctx.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) ?? null;
    }

    private static bool IsMonorailClassSyntaxTargetForGeneration(SyntaxNode syntaxNode)
    {
        return syntaxNode is ClassDeclarationSyntax c
               && c.Modifiers.Any(i => i.IsKind(SyntaxKind.PartialKeyword))
               && c.Identifier.ToString().Equals("MonorailCSS", StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool IsSyntaxTargetForAttributeClassGeneration(SyntaxNode node)
        => node is InvocationExpressionSyntax
           {
               Expression: MemberAccessExpressionSyntax memberAccessExpressionSyntax
           } m
           && m.ArgumentList.Arguments.Count == 3 && memberAccessExpressionSyntax.Name.ToString()
               .Equals("AddAttribute", StringComparison.Ordinal)
           && m.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax literalExpressionSyntax
           && (
               literalExpressionSyntax.ToString().Equals("\"class\"", StringComparison.OrdinalIgnoreCase)
               || literalExpressionSyntax.ToString().Equals("\"cssclass\"", StringComparison.OrdinalIgnoreCase));

    private static bool IsSyntaxTargetForAddMarkupGeneration(SyntaxNode node)
        => node is InvocationExpressionSyntax m && m.ArgumentList.Arguments.Count == 2 &&
           m.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax &&
           memberAccessExpressionSyntax.Name.ToString().Equals("AddMarkupContent", StringComparison.Ordinal) &&
           m.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax;

    private static string[]? GetAttributeClassMethodCall(GeneratorSyntaxContext context)
    {
        var invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;
        var argumentListArguments = invocationExpressionSyntax.ArgumentList.Arguments;

        var argPosition = argumentListArguments.Count == 1 ? 0 : 2;
        return argumentListArguments[argPosition].Expression is LiteralExpressionSyntax literalExpressionSyntax
            ? new[] { literalExpressionSyntax.ToString().Replace("\"", string.Empty) }
            : default;
    }

    private static string[]? GetCssClassesFromAddMarkup(GeneratorSyntaxContext context)
    {
        // forgive me.
        const string RegExPattern = @"class\s*=\s*[\'\""](?<value>[^<]*?)[\'\""]";
        var invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;

        if (invocationExpressionSyntax.ArgumentList.Arguments[1].Expression is not LiteralExpressionSyntax
            literalExpressionSyntax)
        {
            return default;
        }

        if (context.SemanticModel.GetConstantValue(literalExpressionSyntax).Value is not string value)
        {
            return default;
        }

        var matches = Regex.Matches(value, RegExPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var results = new string[matches.Count];
        for (var i = 0; i < matches.Count; i++)
        {
            results[i] = matches[i].Groups["value"].Captures[0].Value;
        }

        return results;
    }

    private static void Execute(
        ((INamedTypeSymbol Symbol, (ImmutableArray<string[]> Left, ImmutableArray<string[]> Right) Right) Left,
            ImmutableArray<string[]> Right) values,
        SourceProductionContext context)
    {
        var classesToGenerate = new HashSet<string>(values.Left.Right.Left.SelectMany(i => i));
        classesToGenerate.UnionWith(values.Left.Right.Right.SelectMany(i => i));
        classesToGenerate.UnionWith(values.Right.SelectMany(i => i));

        var ns = values.Left.Symbol.ContainingNamespace.ToDisplayString();
        var className = values.Left.Symbol.Name;
        var accessibility = values.Left.Symbol.DeclaredAccessibility;
        var isStatic = values.Left.Symbol.IsStatic;
        var source = GenerateExtensionClass(ns, className, accessibility.ToString().ToLowerInvariant(), isStatic,
            classesToGenerate);
        context.AddSource("monorail-css-jit.g.cs", source);
    }


    private static string GenerateExtensionClass(string ns, string className, string accessibility, bool isStatic,
        IEnumerable<string> classesToGenerate)
    {
        var isFirst = true;
        var sb = new StringBuilder();
        sb.Append($@"namespace {ns}
{{
    {accessibility} {(isStatic ? "static " : "")}partial class {className}
    {{
        public static string[] CssClassValues() => new string[] {{
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
}