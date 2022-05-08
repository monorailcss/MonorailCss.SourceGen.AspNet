using System.Text;
using Microsoft.CodeAnalysis;

namespace MonorailCss.SourceGen.AspNet;

[Generator]
public class CssClassValuesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Do a simple filter for monorail
        IncrementalValuesProvider<INamedTypeSymbol> monorailClassDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => Helpers.IsMonorailClassSyntaxTargetForGeneration(s),
                transform: static (ctx, _) => Helpers.GetMonorailsSemanticTargetForGeneration(ctx))
            .Where(static m => m is not null)!;

        context.RegisterSourceOutput(monorailClassDeclarations, static (spc, source) => Execute(source, spc));
    }

    private static void Execute(INamedTypeSymbol symbol, SourceProductionContext spc)
    {
        var ns = symbol.ContainingNamespace.ToDisplayString();
        var className = symbol.Name;
        var accessibility = symbol.DeclaredAccessibility.ToString().ToLower();
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
            output.AddRange(BlazorMarkupValues());
            output.AddRange(BlazorAddClass());
            output.AddRange(CssClassCallValues());
            output.AddRange(CshtmlClasses());
            return output.ToArray();
        }}
    ");
        sb.AppendLine(@"
    }
}
");
        spc.AddSource("monorail-css-jit.g.cs", sb.ToString());
    }
}