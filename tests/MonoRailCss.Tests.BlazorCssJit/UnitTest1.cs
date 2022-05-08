using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Text;
using MonorailCss.SourceGen.AspNet;

namespace MonoRailCss.Tests.BlazorCssJit;

class TestInit
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        MSBuildLocator.RegisterDefaults();
    }
}

public class CustomAdditionalText : AdditionalText
{
    private readonly string _text;

    public override string Path { get; }

    public CustomAdditionalText(string path)
    {
        Path = path;
        _text = File.ReadAllText(path);
    }

    public override SourceText GetText(CancellationToken cancellationToken = new CancellationToken())
    {
        return SourceText.From(_text);
    }
}

public class UnitTest1
{
    [Fact]
    public async Task Test1()
    {
        var projFile = PathTestHelper.GetPath("../MvcTestApp/MvcTestApp.csproj");
        var workspace = MSBuildWorkspace.Create();
        workspace.SkipUnrecognizedProjects = true;
        var project = await workspace.OpenProjectAsync(projFile);
        var compilation = await project.GetCompilationAsync();

        // directly create an instance of the generator
        // (Note: in the compiler this is loaded from an assembly, and created via reflection at runtime)
        var generator = new CshtmlClassGenerator();

        // Create the driver that will control the generation, passing in our generator
        var driver = CSharpGeneratorDriver.Create(generator)
                .AddAdditionalTexts(project.AdditionalDocuments.Select(i => new CustomAdditionalText(i.FilePath!) as AdditionalText).ToImmutableArray())
            ;

        // Run the generation pass
        // (Note: the generator driver itself is immutable, and all calls return an updated version of the driver that you should use for subsequent calls)
        if (compilation != null)
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation,
                out var outputCompilation,
                out var diagnostics);
        }
    }
}
