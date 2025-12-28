using System.Collections.Immutable;
using System.Reflection;

namespace AsyncMediator.SourceGenerator.Tests.TestHelpers;

/// <summary>
/// Helper class for running source generator tests with proper references.
/// </summary>
internal static class GeneratorTestHelper
{
    /// <summary>
    /// Runs the HandlerDiscoveryGenerator and returns the generated sources.
    /// </summary>
    public static (ImmutableArray<GeneratedSourceResult> GeneratedSources, ImmutableArray<Diagnostic> Diagnostics) RunGenerator(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetRequiredReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new HandlerDiscoveryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var runResult = driver.GetRunResult();
        return (runResult.Results[0].GeneratedSources, diagnostics);
    }

    /// <summary>
    /// Verifies that the generated code compiles without errors.
    /// </summary>
    public static void VerifyCompiles(string source)
    {
        var (generatedSources, diagnostics) = RunGenerator(source);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count != 0)
        {
            var errorMessages = string.Join(Environment.NewLine, errors.Select(e => e.GetMessage()));
            Assert.Fail($"Generated code has compilation errors:{Environment.NewLine}{errorMessages}");
        }
    }

    /// <summary>
    /// Gets the generated source with the specified filename.
    /// </summary>
    public static string? GetGeneratedSource(ImmutableArray<GeneratedSourceResult> sources, string filenameContains)
    {
        var source = sources.FirstOrDefault(s => s.HintName.Contains(filenameContains));
        return source.SourceText?.ToString();
    }

    private static List<MetadataReference> GetRequiredReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Task).Assembly,
            typeof(ICommand).Assembly,
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("System.Linq"),
            Assembly.Load("netstandard"),
            Assembly.Load("Microsoft.Extensions.DependencyInjection.Abstractions"),
        };

        return assemblies
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();
    }
}

/// <summary>
/// Provides common test source code snippets.
/// </summary>
internal static class TestSources
{
    public const string CommandHandlerPreamble = """
        using AsyncMediator;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    public static string SimpleCommandHandler(string commandName = "TestCommand", string handlerName = "TestCommandHandler") => $$"""
        {{CommandHandlerPreamble}}
        namespace TestNamespace;

        public class {{commandName}} : ICommand { }

        public class {{handlerName}} : ICommandHandler<{{commandName}}>
        {
            public Task<ICommandWorkflowResult> Handle({{commandName}} command, CancellationToken ct)
                => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
        }
        """;

    public static string EventHandler(string eventName = "TestEvent", string handlerName = "TestEventHandler") => $$"""
        {{CommandHandlerPreamble}}
        namespace TestNamespace;

        public class {{eventName}} : IDomainEvent { }

        public class {{handlerName}} : IEventHandler<{{eventName}}>
        {
            public Task Handle({{eventName}} @event, CancellationToken ct) => Task.CompletedTask;
        }
        """;

    public static string QueryHandler(string criteriaName = "TestCriteria", string resultName = "TestResult", string handlerName = "TestQueryHandler") => $$"""
        {{CommandHandlerPreamble}}
        namespace TestNamespace;

        public class {{criteriaName}} { }
        public class {{resultName}} { }

        public class {{handlerName}} : IQuery<{{criteriaName}}, {{resultName}}>
        {
            public Task<{{resultName}}> Query({{criteriaName}} criteria, CancellationToken ct)
                => Task.FromResult(new {{resultName}}());
        }
        """;

    public static string LookupQueryHandler(string resultName = "TestLookupResult", string handlerName = "TestLookupQueryHandler") => $$"""
        {{CommandHandlerPreamble}}
        namespace TestNamespace;

        public class {{resultName}} { }

        public class {{handlerName}} : ILookupQuery<{{resultName}}>
        {
            public Task<{{resultName}}> Query(CancellationToken ct)
                => Task.FromResult(new {{resultName}}());
        }
        """;

    public static string ExcludedHandler => $$"""
        {{CommandHandlerPreamble}}
        namespace TestNamespace;

        public class ExcludedCommand : ICommand { }

        [ExcludeFromMediator]
        public class ExcludedHandler : ICommandHandler<ExcludedCommand>
        {
            public Task<ICommandWorkflowResult> Handle(ExcludedCommand command, CancellationToken ct)
                => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
        }
        """;

    public static string HandlerWithLifetime(int lifetime) => $$"""
        {{CommandHandlerPreamble}}
        namespace TestNamespace;

        public class LifetimeCommand : ICommand { }

        [MediatorHandler(Lifetime = {{lifetime}})]
        public class LifetimeHandler : ICommandHandler<LifetimeCommand>
        {
            public Task<ICommandWorkflowResult> Handle(LifetimeCommand command, CancellationToken ct)
                => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
        }
        """;

    public static string AbstractHandler => $$"""
        {{CommandHandlerPreamble}}
        namespace TestNamespace;

        public class AbstractCommand : ICommand { }

        public abstract class AbstractBaseHandler : ICommandHandler<AbstractCommand>
        {
            public abstract Task<ICommandWorkflowResult> Handle(AbstractCommand command, CancellationToken ct);
        }
        """;

    public static string DraftMessage(string? reason = null) => reason is null
        ? $$"""
            {{CommandHandlerPreamble}}
            namespace TestNamespace;

            [MediatorDraft]
            public class DraftCommand : ICommand { }
            """
        : $$"""
            {{CommandHandlerPreamble}}
            namespace TestNamespace;

            [MediatorDraft(Reason = "{{reason}}")]
            public class DraftCommand : ICommand { }
            """;
}
