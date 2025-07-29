using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class MonorailCssClassGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Collect();

        var parsedRazorValues = GetParsedRazorValue(context).Collect();
        var addCssClassCall = GetCssClassCallValue(context).Collect();

        // Using Combine to gather all necessary data for both outputs
        var combined = monorailClassDeclarations.Combine(parsedRazorValues).Combine(addCssClassCall);

        // Register the implementation source (not visible to intellisense)
        context.RegisterImplementationSourceOutput(
            combined,
            static (spc, source) => GenerateImplementation(source, spc));

        // Register the public API source (visible to intellisense)
        context.RegisterSourceOutput(
            monorailClassDeclarations,
            static (spc, classDefinitions) => GeneratePublicAPI(classDefinitions, spc));
    }

    private static void GeneratePublicAPI(
        ImmutableArray<MonorailClassDefinition> classDefinitions,
        SourceProductionContext spc)
    {
        if (classDefinitions.Length == 0) return;

        var symbol = classDefinitions.First();
        var ns = symbol.Namespace;
        var className = symbol.Classname;
        var modifiers = symbol.Modifiers;

        if (ns == "<global namespace>" || string.IsNullOrWhiteSpace(ns))
        {
            ns = "Root";
        }

        var publicApi = $$"""
namespace {{ns}}
{
    {{modifiers}} class {{className}}
    {
        /// <summary>
        /// Returns a list of discovered CSS classes from razor files and marked calls.
        /// </summary>
        public static string[] CssClassValues()
        {
            return InternalCssClassValues();
        }
    }
}
""";

        spc.AddSource("monorail-css-jit-public.g.cs", publicApi);
    }

    private static void GenerateImplementation(
        ((ImmutableArray<MonorailClassDefinition> ClassDefinition, ImmutableArray<string[]> CssClasses) Left,
            ImmutableArray<string[]> CssClasses) value,
        SourceProductionContext spc)
    {
        if (value.Left.ClassDefinition.Length == 0) return;

        var symbol = value.Left.ClassDefinition.First();
        var classesToGenerate = ImmutableHashSet.Create<string>();

        var cssClassesFromRazor = value.Left.CssClasses.Sum(i => i.Length);
        var cssClassesFromMethodCalls = value.CssClasses.Sum(i => i.Length);

        foreach (var classes in value.CssClasses)
        {
            classesToGenerate = classesToGenerate.Union(classes);
        }

        foreach (var classes in value.Left.CssClasses)
        {
            classesToGenerate = classesToGenerate.Union(classes);
        }

        var classList = string.Join(", ", classesToGenerate.Select(i => $"\"{i}\""));

        var ns = symbol.Namespace;
        var className = symbol.Classname;
        var modifiers = symbol.Modifiers;

        if (ns == "<global namespace>" || string.IsNullOrWhiteSpace(ns))
        {
            ns = "Root";
        }

        var implementation = $$"""
namespace {{ns}}
{
    {{modifiers}} class {{className}}
    {
        private static string[] _output = new[] {
            {{classList}}
        };

        /// <summary>
        /// Internal implementation that returns a list of discovered CSS classes from razor files and marked calls.
        /// </summary>
        /// <remarks>
        /// <p>Discovered the follow CSS classes:</p>
        /// <ul>
        ///     <li>Razor CSS class attributes: {{cssClassesFromRazor}}.</li>
        ///     <li>Method calls: {{cssClassesFromMethodCalls}}.</li>
        /// </ul>
        /// <p>Generated at {{DateTime.Now.ToString(CultureInfo.InvariantCulture)}}.</p>
        /// </remarks>
        private static string[] InternalCssClassValues() {
            return _output;
        }
    }
}
""";

        spc.AddSource("monorail-css-jit-impl.g.cs", implementation);
    }

    private static IncrementalValuesProvider<string[]> GetCssClassCallValue(
        IncrementalGeneratorInitializationContext context)
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
            var regex =
                """(class\s*=\s*\"(?<value>[^<]*?)\")|(cssclass\s*=\s*\"(?<value>[^<]*?)\")|(CssClass\s*\(\s*\"(?<value>[^<]*?)\"\s*\))""";
            var additionalFileFilter = new[] { ".cshtml", ".razor" };

            if (provider.GlobalOptions.TryGetValue("fileparsergenerator_razor_regex", out var configValue))
            {
                regex = configValue;
            }

            if (provider.GlobalOptions.TryGetValue("fileparsergenerator_razor_file_extensions", out var fileFilter))
            {
                additionalFileFilter = fileFilter.Split('|');
            }

            return (Regex: regex, Filter: additionalFileFilter);
        });
        return config;
    }

    private static string[]? GetCssClassMethodMethodCall(GeneratorSyntaxContext ctx)
    {
        var invocationExpressionSyntax = (InvocationExpressionSyntax)ctx.Node;
        var argumentListArguments = invocationExpressionSyntax.ArgumentList.Arguments;

        return argumentListArguments[0].Expression is LiteralExpressionSyntax literalExpressionSyntax
            ? [literalExpressionSyntax.ToString().Replace("\"", string.Empty)]
            : default;
    }

    private static bool IsSyntaxTargetForCssClassCallGeneration(SyntaxNode syntaxNode)
    {
        if (syntaxNode is not InvocationExpressionSyntax i)
        {
            return false;
        }

        if (i.ArgumentList.Arguments.Count == 0 || i.ArgumentList.Arguments[0].Expression is not LiteralExpressionSyntax)
        {
            return false;
        }

        if (i.Expression.ToString().Equals("CssClass", StringComparison.Ordinal) ||
            i.Expression.ToString().Equals("AddClass", StringComparison.Ordinal))
        {
            return true;
        }

        if (i.Expression is not MemberAccessExpressionSyntax me)
        {
            return false;
        }

        return me.Name.ToString().Equals("CssClass", StringComparison.Ordinal) ||
               me.Name.ToString().Equals("AddClass", StringComparison.Ordinal);
    }
}