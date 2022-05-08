using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class BlazorAddMarkupGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        // and finally if there is a call to add Markup then we need to parse that whole string looking for css classes
        IncrementalValuesProvider<string[]> addMarkup = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForAddMarkupGeneration(s),
                transform: static (ctx, _) => GetCssClassesFromAddMarkup(ctx))
            .Where(static m => m is not null)!;

        var allAddClassAttributes =
            monorailClassDeclarations.Combine(addMarkup.Collect());

        context.RegisterSourceOutput(allAddClassAttributes, static (spc, source) => Execute(source, spc));
    }

    private static bool IsSyntaxTargetForAddMarkupGeneration(SyntaxNode node)
        => node is InvocationExpressionSyntax m && m.ArgumentList.Arguments.Count == 2 &&
           m.Expression is MemberAccessExpressionSyntax memberAccessExpressionSyntax &&
           memberAccessExpressionSyntax.Name.ToString().Equals("AddMarkupContent", StringComparison.Ordinal) &&
           m.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax;

    private static string[]? GetCssClassesFromAddMarkup(GeneratorSyntaxContext context)
    {
        var invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;

        if (invocationExpressionSyntax.ArgumentList.Arguments[1].Expression is not LiteralExpressionSyntax
            literalExpressionSyntax)
        {
            return default;
        }

        return context.SemanticModel.GetConstantValue(literalExpressionSyntax).Value is not string value
            ? default
            : Helpers.GetCssClassFromHtml(value);
    }

    private static void Execute(
        (INamedTypeSymbol Left, ImmutableArray<string[]> Right) values,
        SourceProductionContext context)
    {
        var classesToGenerate = new HashSet<string>(values.Right.SelectMany(i => i));

        var source = Helpers.GenerateExtensionClass(values.Left, "BlazorMarkupValues", classesToGenerate);
        context.AddSource("monorail-css-blazor-markup-jit.g.cs", source);
    }
}