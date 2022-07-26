using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class MonorailCssClassGenerator : IIncrementalGenerator
{
    private const string CssClassCallGeneratorGeneratedMethodName = "GetCssClasses";
    private const string FileParserGeneratorGeneratedMethodName = "GetFileParser";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Collect();

        var parsedRazorValues = GetParsedRazorValue(context);
        var addCssClassCall = GetCssClassCallValue(context);

        context.RegisterSourceOutput(
            monorailClassDeclarations,
            static (spc, source) => ExecuteAddMain(source, spc));
        context.RegisterSourceOutput(
            addCssClassCall.Collect().Combine(monorailClassDeclarations),
            static (spc, source) => ExecuteAddCssClass(source, spc));
        context.RegisterSourceOutput(
            parsedRazorValues.Collect().Combine(monorailClassDeclarations),
            static (spc, source) => ExecuteParsedRazor(source, spc));
    }

    private static IncrementalValuesProvider<string[]> GetCssClassCallValue(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<string[]> addCssClassCall = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsSyntaxTargetForCssClassCallGeneration(s),
                transform: static (ctx, _) => GetCssClassMethodMethodCall(ctx))
            .Where(static m => m is not null)!;
        return addCssClassCall;
    }

    private static IncrementalValuesProvider<string[]> GetParsedRazorValue(
        IncrementalGeneratorInitializationContext context)
    {
        var config = GetConfigValue(context);

        var namesAndContents = context.AdditionalTextsProvider
            .Combine(config)
            .Where(static value =>
            {
                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var filter in value.Right.Filter)
                {
                    File.AppendAllText("r:\\test.txt", value.Left.Path + Environment.NewLine);
                    if (value.Left.Path.EndsWith(filter))
                    {
                        return true;
                    }
                }

                return false;
            })
            .Select(static (value, token) => (Config: value.Right, Content: value.Left.GetText(token)!.ToString()))
            .Select(static (value, _) => Helpers.GetCssClassFromHtml(value.Content, value.Config.Regex));
        return namesAndContents;
    }

    private static IncrementalValueProvider<(string Regex, string[] Filter)> GetConfigValue(
        IncrementalGeneratorInitializationContext context)
    {
        var config = context.AnalyzerConfigOptionsProvider.Select((provider, _) =>
        {
            // language=regex
            var regex = @"(class\s*=\s*[\'\""](?<value>[^<]*?)[\'\""])|(cssclass\s*=\s*[\'\""](?<value>[^<]*?)[\'\""])|(CssClass\s*\(\s*\""(?<value>[^<]*?)\""\s*\))";
            var additionalFileFilter = new[] { ".cshtml", ".razor" };

            if (provider.GlobalOptions.TryGetValue("fileparsergenerator_razor_regex", out var configValue))
            {
                regex = configValue;
            }

            if (provider.GlobalOptions.TryGetValue("fileparsergenerator_razor_file_extensions", out var fileFilter))
            {
                additionalFileFilter = fileFilter.Split('|');
            }

            var contents = $"filter: {additionalFileFilter}, regex: {regex}{Environment.NewLine}";
            File.AppendAllText("r:\\test.txt", contents);

            return (Regex: regex, Filter: additionalFileFilter);
        });
        return config;
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

    private static void ExecuteAddCssClass(
        (ImmutableArray<string[]> Classes, ImmutableArray<MonorailClassDefinition> Symbols) values,
        SourceProductionContext context)
    {
        if (values.Symbols.Length == 0) return;

        var symbol = values.Symbols.First();
        var classesToGenerate = ImmutableHashSet.Create<string>();

        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (var classes in values.Classes)
        {
            classesToGenerate = classesToGenerate.Union(classes);
        }

        var source = Helpers.GenerateExtensionClass(symbol, CssClassCallGeneratorGeneratedMethodName, classesToGenerate);
        context.AddSource( "monorail-css-jit-cssclass.g.cs", source);
    }

    private static void ExecuteParsedRazor(
        (ImmutableArray<string[]> Classes, ImmutableArray<MonorailClassDefinition> Symbols) values,
        SourceProductionContext spc)
    {
        if (values.Symbols.Length == 0) return;

        var symbol = values.Symbols.First();

        var classesToGenerate = ImmutableHashSet.Create<string>();
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (var classes in values.Classes)
        {
            classesToGenerate = classesToGenerate.Union(classes);
        }

        var source = Helpers.GenerateExtensionClass(symbol, FileParserGeneratorGeneratedMethodName, classesToGenerate);
        spc.AddSource("monorail-css-jit-razor.g.cs", source);
    }

    private static void ExecuteAddMain(ImmutableArray<MonorailClassDefinition> symbols, SourceProductionContext spc)
    {
        if (symbols.Length == 0) return;

        var symbol = symbols.First();

        var ns = symbol.Namespace;
        var className = symbol.Classname;
        var modifiers = symbol.Modifiers;

        if (ns == "<global namespace>")
        {
            ns = "Root";
        }

        var s = $$$"""
namespace {{{ns}}}
{
    {{{modifiers}}} class {{{className}}}
    {
        public static string[] CssClassValues() {
            var output = new List<string>();
            output.AddRange({{{CssClassCallGeneratorGeneratedMethodName}}}());
            output.AddRange({{{FileParserGeneratorGeneratedMethodName}}}());

            return output.ToArray();
        }
    }
}
""";

        spc.AddSource("monorail-css-jit.g.cs", s);
    }
}