using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class CssClassCallGenerator : IIncrementalGenerator
{
    internal const string GeneratedMethodName = "CssClassCallValues";
    private const string GeneratedFileName = "monorail-css-cssclass-jit.g.cs";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        IncrementalValuesProvider<string[]> addCssClassCall = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForCssClassCallGeneration(s),
                transform: static (ctx, _) => GetCssClassMethodMethodCall(ctx))
            .Where(static m => m is not null)!;


        var allAddClassAttributes =
            monorailClassDeclarations.Combine(addCssClassCall.Collect());

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

    private static void Execute(
        (INamedTypeSymbol Left, ImmutableArray<string[]> Right) values,
        SourceProductionContext context)
    {
        var classesToGenerate = new HashSet<string>(values.Right.SelectMany(i => i));

        var source = Helpers.GenerateExtensionClass(values.Left, GeneratedMethodName, classesToGenerate);
        context.AddSource(GeneratedFileName, source);
    }
}