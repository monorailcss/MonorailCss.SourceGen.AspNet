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

        // find all additional files that end with .cshtml or .razor
        var razorFiles = context.AdditionalTextsProvider
            .Where(static file => file.Path.EndsWith(".cshtml") || file.Path.EndsWith(".razor"))
            .Select((text, _) => Helpers.GetGeneratedMethodName(text.Path));

        context.RegisterSourceOutput(monorailClassDeclarations.Collect().Combine(razorFiles.Collect()), static (spc, source) => Execute(source, spc));
    }

    private static void Execute((ImmutableArray<INamedTypeSymbol> Left, ImmutableArray<string> Right) values, SourceProductionContext spc)
    {
        var symbol = values.Left.FirstOrDefault();
        if (symbol == null)
        {
            return;
        }

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
            // output.AddRange({BlazorAddMarkupGenerator.GeneratedMethodName}());
            // output.AddRange({BlazorAddClassAttributeGenerator.GeneratedMethodName}());
            output.AddRange({CssClassCallGenerator.GeneratedMethodName}());
            ");

        foreach (var methodName in values.Right)
        {
            sb.AppendLine($"output.AddRange({methodName}());");
        }

        sb.AppendLine(@"
            return output.ToArray();
        }
    ");
        sb.AppendLine(@"
    }
}
");
        spc.AddSource(GeneratedFileName, sb.ToString());
    }
}