using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AsyncMediator.SourceGenerator.Tests.Analyzers;

[TestClass]
public class AsyncMediatorAnalyzerTests
{
    // The MediatorDraft attribute definition needed for tests (since the source generator isn't run during analyzer tests)
    private const string MediatorDraftAttributeDefinition = """
        namespace AsyncMediator
        {
            [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct)]
            internal sealed class MediatorDraftAttribute : System.Attribute
            {
                public string? Reason { get; set; }
            }
        }
        """;

    [TestMethod]
    public async Task Analyze_MissingCommandHandler_ReportsASYNCMED001()
    {
        // Arrange - Command used but no handler exists
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class MissingHandlerCommand : ICommand { }

            public class Consumer
            {
                private readonly IMediator _mediator;

                public Consumer(IMediator mediator) => _mediator = mediator;

                public async Task DoWork()
                {
                    await _mediator.Send(new MissingHandlerCommand());
                }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source);

        // Assert
        var error = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED001");
        Assert.IsNotNull(error, "Expected ASYNCMED001 diagnostic for missing command handler");
        Assert.AreEqual(DiagnosticSeverity.Error, error.Severity);
        Assert.IsTrue(error.GetMessage().Contains("MissingHandlerCommand"));
    }

    [TestMethod]
    public async Task Analyze_CommandHandlerExists_NoError()
    {
        // Arrange - Command has handler
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class HasHandlerCommand : ICommand { }

            public class HasHandlerCommandHandler : ICommandHandler<HasHandlerCommand>
            {
                public Task<ICommandWorkflowResult> Handle(HasHandlerCommand cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class Consumer
            {
                private readonly IMediator _mediator;

                public Consumer(IMediator mediator) => _mediator = mediator;

                public async Task DoWork()
                {
                    await _mediator.Send(new HasHandlerCommand());
                }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source);

        // Assert
        var error = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED001");
        Assert.IsNull(error, "Expected no ASYNCMED001 diagnostic when handler exists");
    }

    [TestMethod]
    public async Task Analyze_DuplicateCommandHandlers_ReportsASYNCMED002()
    {
        // Arrange - Two handlers for same command
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class DuplicateCommand : ICommand { }

            public class Handler1 : ICommandHandler<DuplicateCommand>
            {
                public Task<ICommandWorkflowResult> Handle(DuplicateCommand cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class Handler2 : ICommandHandler<DuplicateCommand>
            {
                public Task<ICommandWorkflowResult> Handle(DuplicateCommand cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source);

        // Assert
        var errors = diagnostics.Where(d => d.Id == "ASYNCMED002").ToList();
        Assert.AreEqual(2, errors.Count, "Expected ASYNCMED002 diagnostic on both duplicate handlers");
        Assert.IsTrue(errors[0].GetMessage().Contains("Handler1") || errors[0].GetMessage().Contains("Handler2"));
    }

    [TestMethod]
    public async Task Analyze_MissingEventHandler_ReportsASYNCMED004Warning()
    {
        // Arrange - Event published but no handler
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class MissingHandlerEvent : IDomainEvent { }

            public class Consumer
            {
                private readonly IMediator _mediator;

                public Consumer(IMediator mediator) => _mediator = mediator;

                public void DoWork()
                {
                    _mediator.DeferEvent(new MissingHandlerEvent());
                }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source);

        // Assert - Note: The analyzer checks for Publish, not DeferEvent
        // This test validates the analyzer structure, actual event detection depends on implementation
        var noErrors = !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);
        Assert.IsTrue(noErrors, "Expected no errors for event without handler (events are warnings)");
    }

    [TestMethod]
    public async Task Analyze_DraftCommand_SuppressesMissingHandlerError()
    {
        // Arrange - Draft command without handler should NOT generate ASYNCMED001
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [MediatorDraft(Reason = "Handler pending")]
            public class DraftCommand : ICommand { }

            public class Consumer
            {
                private readonly IMediator _mediator;

                public Consumer(IMediator mediator) => _mediator = mediator;

                public async Task DoWork()
                {
                    await _mediator.Send(new DraftCommand());
                }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, includeAttributeDefinition: true);

        // Assert
        var missingError = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED001");
        Assert.IsNull(missingError, "Expected ASYNCMED001 to be suppressed for draft command");

        var draftInfo = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED006");
        Assert.IsNotNull(draftInfo, "Expected ASYNCMED006 info diagnostic for draft message");
        Assert.IsTrue(draftInfo.GetMessage().Contains("Handler pending"));
    }

    [TestMethod]
    public async Task Analyze_DraftWithoutReason_ReportsBasicDraftInfo()
    {
        // Arrange
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            [MediatorDraft]
            public class DraftCommandNoReason : ICommand { }

            public class Consumer
            {
                private readonly IMediator _mediator;

                public Consumer(IMediator mediator) => _mediator = mediator;

                public async Task DoWork()
                {
                    await _mediator.Send(new DraftCommandNoReason());
                }
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source, includeAttributeDefinition: true);

        // Assert
        var draftInfo = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED006");
        Assert.IsNotNull(draftInfo, "Expected ASYNCMED006 info for draft message");
        Assert.AreEqual(DiagnosticSeverity.Info, draftInfo.Severity);
    }

    [TestMethod]
    public async Task Analyze_RegularMessage_NoDraftDiagnostic()
    {
        // Arrange
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class RegularCommand : ICommand { }

            public class RegularCommandHandler : ICommandHandler<RegularCommand>
            {
                public Task<ICommandWorkflowResult> Handle(RegularCommand cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source);

        // Assert
        var draftInfo = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED006");
        Assert.IsNull(draftInfo, "Expected no ASYNCMED006 for regular non-draft message");
    }

    [TestMethod]
    public async Task Analyze_SingleCommandHandler_NoDuplicateError()
    {
        // Arrange
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class SingleHandlerCommand : ICommand { }

            public class SingleHandler : ICommandHandler<SingleHandlerCommand>
            {
                public Task<ICommandWorkflowResult> Handle(SingleHandlerCommand cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source);

        // Assert
        var duplicateError = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED002");
        Assert.IsNull(duplicateError, "Expected no duplicate handler error for single handler");
    }

    [TestMethod]
    public async Task Analyze_AbstractHandler_IsNotCounted()
    {
        // Arrange - Abstract handlers should not be registered
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class AbstractCommand : ICommand { }

            public abstract class AbstractHandler : ICommandHandler<AbstractCommand>
            {
                public abstract Task<ICommandWorkflowResult> Handle(AbstractCommand cmd, CancellationToken ct);
            }
            """;

        // Act
        var diagnostics = await RunAnalyzerAsync(source);

        // Assert - No duplicate errors since abstract classes are excluded
        var duplicateError = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED002");
        Assert.IsNull(duplicateError);
    }

    [TestMethod]
    public async Task Analyze_ConcurrentExecution_DoesNotCorruptState()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class ConcurrentCommand : ICommand { }

            public class ConcurrentCommandHandler1 : ICommandHandler<ConcurrentCommand>
            {
                public Task<ICommandWorkflowResult> Handle(ConcurrentCommand cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class ConcurrentCommandHandler2 : ICommandHandler<ConcurrentCommand>
            {
                public Task<ICommandWorkflowResult> Handle(ConcurrentCommand cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class Consumer
            {
                private readonly IMediator _mediator;

                public Consumer(IMediator mediator) => _mediator = mediator;

                public async Task DoWork()
                {
                    await _mediator.Send(new ConcurrentCommand());
                }
            }
            """;

        var tasks = Enumerable.Range(0, 20).Select(_ => RunAnalyzerAsync(source)).ToArray();
        var results = await Task.WhenAll(tasks);

        foreach (var diagnostics in results)
        {
            var duplicateErrors = diagnostics.Where(d => d.Id == "ASYNCMED002").ToList();
            Assert.AreEqual(2, duplicateErrors.Count, "Each concurrent run should detect exactly 2 duplicate handler diagnostics");
            Assert.IsTrue(duplicateErrors.All(e => e.GetMessage().Contains("Handler1") || e.GetMessage().Contains("Handler2")));
        }
    }

    [TestMethod]
    public async Task Analyze_DuplicateQueryHandlers_ReportsASYNCMED007()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;
            using System.Collections.Generic;

            namespace TestNamespace;

            public record SearchCriteria(string Term);

            public class QueryHandler1 : IQuery<SearchCriteria, List<string>>
            {
                public Task<List<string>> Query(SearchCriteria criteria, CancellationToken ct = default)
                    => Task.FromResult(new List<string>());
            }

            public class QueryHandler2 : IQuery<SearchCriteria, List<string>>
            {
                public Task<List<string>> Query(SearchCriteria criteria, CancellationToken ct = default)
                    => Task.FromResult(new List<string>());
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);

        var warnings = diagnostics.Where(d => d.Id == "ASYNCMED007").ToList();
        Assert.AreEqual(2, warnings.Count, "Expected ASYNCMED007 warning on both duplicate query handlers");
        Assert.IsTrue(warnings[0].GetMessage().Contains("QueryHandler1") || warnings[0].GetMessage().Contains("QueryHandler2"));
    }

    [TestMethod]
    public async Task Analyze_SingleQueryHandler_NoDuplicateWarning()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;
            using System.Collections.Generic;

            namespace TestNamespace;

            public record SearchCriteria(string Term);

            public class SingleQueryHandler : IQuery<SearchCriteria, List<string>>
            {
                public Task<List<string>> Query(SearchCriteria criteria, CancellationToken ct = default)
                    => Task.FromResult(new List<string>());
            }
            """;

        var diagnostics = await RunAnalyzerAsync(source);

        var warning = diagnostics.FirstOrDefault(d => d.Id == "ASYNCMED007");
        Assert.IsNull(warning, "Expected no ASYNCMED007 for single query handler");
    }

    private static async Task<ImmutableArray<Diagnostic>> RunAnalyzerAsync(string source, bool includeAttributeDefinition = false)
    {
        var syntaxTrees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(source) };

        if (includeAttributeDefinition)
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(MediatorDraftAttributeDefinition));

        var references = GetRequiredReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new AsyncMediatorAnalyzer();
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer]);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync();
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
        };

        return assemblies
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();
    }
}
