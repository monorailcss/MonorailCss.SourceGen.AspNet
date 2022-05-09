using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class BlazorAddClassAttributeGenerator : IIncrementalGenerator
{
    internal const string GeneratedMethodName = "BlazorAddClass";
    private const string GeneratedFileName = "monorail-css-blazor-add-class-jit.g.cs";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        // is this a call for AddAttribute where the attribute is named "class" or "cssclass"?
        IncrementalValuesProvider<string[]> addClassAttribute = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForAttributeClassGeneration(s),
                transform: static (ctx, _) => GetAttributeClassMethodCall(ctx))
            .Where(static m => m is not null)!;

        var allAddClassAttributes =
            monorailClassDeclarations.Combine(addClassAttribute.Collect());

        context.RegisterSourceOutput(allAddClassAttributes, static (spc, source) => Execute(source, spc));
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

    private static string[]? GetAttributeClassMethodCall(GeneratorSyntaxContext context)
    {
        var invocationExpressionSyntax = (InvocationExpressionSyntax)context.Node;
        var argumentListArguments = invocationExpressionSyntax.ArgumentList.Arguments;

        var argPosition = argumentListArguments.Count == 1 ? 0 : 2;
        return argumentListArguments[argPosition].Expression is LiteralExpressionSyntax literalExpressionSyntax
            ? new[] { literalExpressionSyntax.ToString().Replace("\"", string.Empty) }
            : default;
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