using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class FileParserGenerator: IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        var namesAndContents = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".cshtml") || file.Path.EndsWith(".razor"))
            .Select((file, token) => (file.Path, Content: file.GetText(token)!.ToString()))
            .Select((value, _) => (value.Path, Classes: Helpers.GetCssClassFromHtml(value.Content)));

        var allAddClassAttributes = namesAndContents.Combine(monorailClassDeclarations.Collect());

        context.RegisterSourceOutput(allAddClassAttributes, static (spc, source) => Execute(source, spc));
    }

    private static void Execute(((string Path, string[] Classes) Left, ImmutableArray<INamedTypeSymbol> Symbols) values, SourceProductionContext spc)
    {
        var symbol = values.Symbols.FirstOrDefault();
        if (symbol == default)
        {
            return;
        }

        var classesToGenerate = new HashSet<string>(values.Left.Classes);
        var path = values.Left.Path;
        var generatedFileName = Helpers.GetGeneratedFileName(path);
        var generatedMethodName = Helpers.GetGeneratedMethodName(path);
        var source = Helpers.GenerateExtensionClass(symbol, generatedMethodName, classesToGenerate);
        spc.AddSource(generatedFileName, source);
    }
}