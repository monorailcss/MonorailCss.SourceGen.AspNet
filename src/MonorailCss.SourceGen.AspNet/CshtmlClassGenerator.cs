using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class CshtmlClassGenerator: IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        // find all additional files that end with .txt
        var textFiles = context.AdditionalTextsProvider.Where(static file => file.Path.EndsWith(".cshtml"));

        // read their contents and save their name
        var namesAndContents = textFiles
            .Select((text, cancellationToken) =>
                Helpers.GetCssClassFromHtml(text.GetText(cancellationToken)!.ToString()));

        var allAddClassAttributes = monorailClassDeclarations.Combine(namesAndContents.Collect());


        context.RegisterSourceOutput(allAddClassAttributes, static (spc, source) => Execute(source, spc));
    }

    private static void Execute((INamedTypeSymbol Left, ImmutableArray<string[]> Right) values, SourceProductionContext spc)
    {
        var classesToGenerate = new HashSet<string>(values.Right.SelectMany(i => i));

        var source = Helpers.GenerateExtensionClass(values.Left, "CshtmlClasses", classesToGenerate);
        spc.AddSource("monorail-css-cshtml-jit.g.cs", source);
    }
}