using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class FileParserGenerator: IIncrementalGenerator
{
    public const string GeneratedMethodName = "GetFileParser";
    private const string Path = "monorail-css-file-parser-.g.cs";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var config = context.AnalyzerConfigOptionsProvider.Select((provider, _) =>
        {
            var exists = provider.GlobalOptions.TryGetValue("fileparsergenerator_razor_regex",
                out var configValue);

            return exists
                ? configValue!
                : @"(class\s*=\s*[\'\""](?<value>[^<]*?)[\'\""])|((cssclass\s*=\s*[\'\""](?<value>[^<]*?)[\'\""]))|(CssClass\s*\(\s*\""(?<value>[^<]*?)\""\s*\))";
        });

        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        var namesAndContents = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".cshtml") || file.Path.EndsWith(".razor"))
            .Combine(config)
            .Select((value, token) => (Regex: value.Right, Content: value.Left.GetText(token)!.ToString()))
            .Select((value, _) =>  Helpers.GetCssClassFromHtml(value.Content, value.Regex));

        var allAddClassAttributes = namesAndContents.Collect().Combine(monorailClassDeclarations.Collect());

        context.RegisterSourceOutput(allAddClassAttributes, static (spc, source) => Execute(source, spc));
    }

    private static void Execute((ImmutableArray<string[]> Classes, ImmutableArray<INamedTypeSymbol> Symbols) values, SourceProductionContext spc)
    {
        var symbol = values.Symbols.FirstOrDefault();
        if (symbol == default)
        {
            return;
        }

        var classesToGenerate = new HashSet<string>();
        foreach (var classes in values.Classes)
        {
            classesToGenerate.UnionWith(classes);
        }

        var source = Helpers.GenerateExtensionClass(symbol, GeneratedMethodName, classesToGenerate);
        spc.AddSource(Path, source);
    }
}