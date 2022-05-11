using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class CssClassValuesGenerator : IIncrementalGenerator
{
    private const string GeneratedFileName = "monorail-css-jit.g.cs";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        context.RegisterSourceOutput(monorailClassDeclarations.Collect(), static (spc, source) => Execute(source, spc));
    }

    private static void Execute(ImmutableArray<INamedTypeSymbol> symbols, SourceProductionContext spc)
    {
        var symbol = symbols.FirstOrDefault();
        if (symbol == null)
        {
            return;
        }

        var ns = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        var accessibility = Helpers.GetAccessibility(symbol);
        var isStatic = symbol.IsStatic;

        var sb = new StringBuilder();
        if (ns == "<global namespace>")
        {
            ns = "Root";
        }

        sb.AppendLine($@"namespace {ns}
{{
    {accessibility} {(isStatic ? "static " : "")}partial class {className}
    {{
        public static string[] CssClassValues() {{
            var output = new List<string>();
            output.AddRange({CssClassCallGenerator.GeneratedMethodName}());
            output.AddRange({FileParserGenerator.GeneratedMethodName}());

            return output.ToArray();
        }}
    ");
        sb.AppendLine(@"
    }
}
");
        spc.AddSource(GeneratedFileName, sb.ToString());
    }
}