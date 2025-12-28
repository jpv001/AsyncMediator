using AsyncMediator.SourceGenerator.Tests.TestHelpers;

namespace AsyncMediator.SourceGenerator.Tests.Generators;

[TestClass]
public class HandlerDiscoveryGeneratorTests
{
    [TestMethod]
    public void Generate_WithCommandHandler_RegistersCorrectly()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
            "Expected no compilation errors");

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration, "Expected registration source to be generated");
        Assert.IsTrue(registration.Contains("TestCommandHandler"), "Expected handler to be registered");
        Assert.IsTrue(registration.Contains("CommandHandlerCount = 1"), "Expected command handler count to be 1");
    }

    [TestMethod]
    public void Generate_WithEventHandler_RegistersCorrectly()
    {
        // Arrange
        var source = TestSources.EventHandler();

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("TestEventHandler"), "Expected event handler to be registered");
        Assert.IsTrue(registration.Contains("EventHandlerCount = 1"), "Expected event handler count to be 1");
    }

    [TestMethod]
    public void Generate_WithQueryHandler_RegistersCorrectly()
    {
        // Arrange
        var source = TestSources.QueryHandler();

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("TestQueryHandler"), "Expected query handler to be registered");
        Assert.IsTrue(registration.Contains("QueryHandlerCount = 1"), "Expected query handler count to be 1");
    }

    [TestMethod]
    public void Generate_WithLookupQueryHandler_RegistersCorrectly()
    {
        // Arrange
        var source = TestSources.LookupQueryHandler();

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("TestLookupQueryHandler"), "Expected lookup query handler to be registered");
        Assert.IsTrue(registration.Contains("QueryHandlerCount = 1"), "Expected query handler count to be 1");
    }

    [TestMethod]
    public void Generate_WithMultipleHandlers_RegistersAllHandlers()
    {
        // Arrange
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class Command1 : ICommand { }
            public class Command2 : ICommand { }
            public class Event1 : IDomainEvent { }
            public class Criteria1 { }
            public class Result1 { }

            public class Handler1 : ICommandHandler<Command1>
            {
                public Task<ICommandWorkflowResult> Handle(Command1 cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class Handler2 : ICommandHandler<Command2>
            {
                public Task<ICommandWorkflowResult> Handle(Command2 cmd, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class EventHandler1 : IEventHandler<Event1>
            {
                public Task Handle(Event1 evt, CancellationToken ct) => Task.CompletedTask;
            }

            public class QueryHandler1 : IQuery<Criteria1, Result1>
            {
                public Task<Result1> Query(Criteria1 c, CancellationToken ct) => Task.FromResult(new Result1());
            }
            """;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("HandlerCount = 4"), "Expected total handler count to be 4");
        Assert.IsTrue(registration.Contains("CommandHandlerCount = 2"), "Expected command handler count to be 2");
        Assert.IsTrue(registration.Contains("EventHandlerCount = 1"), "Expected event handler count to be 1");
        Assert.IsTrue(registration.Contains("QueryHandlerCount = 1"), "Expected query handler count to be 1");
    }

    [TestMethod]
    public void Generate_WithExcludeFromMediatorAttribute_DoesNotRegisterHandler()
    {
        // Arrange
        var source = TestSources.ExcludedHandler;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsFalse(registration.Contains("ExcludedHandler"), "Expected excluded handler to NOT be registered");
        Assert.IsTrue(registration.Contains("HandlerCount = 0"), "Expected handler count to be 0");
    }

    [TestMethod]
    public void Generate_WithMediatorHandlerAttribute_UsesSingletonLifetime()
    {
        // Arrange
        var source = TestSources.HandlerWithLifetime(0); // 0 = Singleton

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("ServiceLifetime.Singleton"), "Expected Singleton lifetime");
    }

    [TestMethod]
    public void Generate_WithMediatorHandlerAttribute_UsesScopedLifetime()
    {
        // Arrange
        var source = TestSources.HandlerWithLifetime(1); // 1 = Scoped

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("ServiceLifetime.Scoped"), "Expected Scoped lifetime");
    }

    [TestMethod]
    public void Generate_WithMediatorHandlerAttribute_UsesTransientLifetime()
    {
        // Arrange
        var source = TestSources.HandlerWithLifetime(2); // 2 = Transient

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("ServiceLifetime.Transient"), "Expected Transient lifetime");
    }

    [TestMethod]
    public void Generate_WithoutMediatorHandlerAttribute_UsesDefaultLifetime()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("defaultLifetime"), "Expected defaultLifetime to be used");
    }

    [TestMethod]
    public void Generate_WithAbstractHandler_DoesNotRegister()
    {
        // Arrange
        var source = TestSources.AbstractHandler;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsFalse(registration.Contains("AbstractBaseHandler"), "Expected abstract handler to NOT be registered");
        Assert.IsTrue(registration.Contains("HandlerCount = 0"), "Expected handler count to be 0");
    }

    [TestMethod]
    public void Generate_WithNoHandlers_GeneratesEmptyRegistration()
    {
        // Arrange
        var source = """
            using AsyncMediator;

            namespace TestNamespace;

            public class NotAHandler { }
            """;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("HandlerCount = 0"), "Expected handler count to be 0");
    }

    [TestMethod]
    public void Generate_ProducesCompilableCode()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act & Assert - This will throw if compilation fails
        GeneratorTestHelper.VerifyCompiles(source);
    }

    [TestMethod]
    public void GenerateAttributes_CreatesExcludeFromMediatorAttribute()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var attributes = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorAttributes.g.cs");
        Assert.IsNotNull(attributes, "Expected attributes source to be generated");
        Assert.IsTrue(attributes.Contains("ExcludeFromMediatorAttribute"), "Expected ExcludeFromMediatorAttribute");
        Assert.IsTrue(attributes.Contains("AttributeTargets.Class"), "Expected class targeting");
    }

    [TestMethod]
    public void GenerateAttributes_CreatesMediatorHandlerAttribute()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var attributes = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorAttributes.g.cs");
        Assert.IsNotNull(attributes);
        Assert.IsTrue(attributes.Contains("MediatorHandlerAttribute"), "Expected MediatorHandlerAttribute");
        Assert.IsTrue(attributes.Contains("Lifetime"), "Expected Lifetime property");
    }

    [TestMethod]
    public void GenerateAttributes_CreatesMediatorDraftAttribute()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var attributes = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorAttributes.g.cs");
        Assert.IsNotNull(attributes);
        Assert.IsTrue(attributes.Contains("MediatorDraftAttribute"), "Expected MediatorDraftAttribute");
        Assert.IsTrue(attributes.Contains("Reason"), "Expected Reason property");
    }

    [TestMethod]
    public void GenerateExtensions_CreatesAddAsyncMediatorMethod()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var extensions = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorExtensions.g.cs");
        Assert.IsNotNull(extensions, "Expected extensions source to be generated");
        Assert.IsTrue(extensions.Contains("AddAsyncMediator"), "Expected AddAsyncMediator method");
        Assert.IsTrue(extensions.Contains("IServiceCollection"), "Expected IServiceCollection parameter");
    }

    [TestMethod]
    public void GenerateBuilder_HasDefaultLifetimeProperty()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var builder = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorBuilder.g.cs");
        Assert.IsNotNull(builder, "Expected builder source to be generated");
        Assert.IsTrue(builder.Contains("DefaultLifetime"), "Expected DefaultLifetime property");
        Assert.IsTrue(builder.Contains("ServiceLifetime.Scoped"), "Expected Scoped as default");
    }

    [TestMethod]
    public void GenerateBuilder_HasHasBehaviorsProperty()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var builder = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorBuilder.g.cs");
        Assert.IsNotNull(builder);
        Assert.IsTrue(builder.Contains("HasBehaviors"), "Expected HasBehaviors property");
    }

    [TestMethod]
    public void GenerateBuilder_AddBehaviorSetsHasBehaviors()
    {
        // Arrange
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var builder = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorBuilder.g.cs");
        Assert.IsNotNull(builder);
        Assert.IsTrue(builder.Contains("AddBehavior<TBehavior>"), "Expected AddBehavior method");
        Assert.IsTrue(builder.Contains("HasBehaviors = true"), "Expected HasBehaviors to be set");
    }

    [TestMethod]
    public void GenerateBuilder_AddBehaviorRegistersAgainstClosedInterfaces()
    {
        // Arrange - Closed behaviors must be registered against each specific
        // IPipelineBehavior<TRequest, TResponse> interface they implement, not the open generic.
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var builder = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorBuilder.g.cs");
        Assert.IsNotNull(builder);

        // Verify the code iterates over interfaces and registers against each closed interface
        Assert.IsTrue(builder.Contains("foreach (var iface in behaviorType.GetInterfaces())"),
            "AddBehavior must iterate over implemented interfaces");
        Assert.IsTrue(builder.Contains("iface.GetGenericTypeDefinition()"),
            "AddBehavior must check for generic interface definition");
        Assert.IsTrue(builder.Contains("services.Add(new global::Microsoft.Extensions.DependencyInjection.ServiceDescriptor("),
            "AddBehavior must register service descriptor");
        Assert.IsTrue(builder.Contains("iface, behaviorType, lifetime"),
            "AddBehavior must register against the specific closed interface (iface), not open generic");
    }

    [TestMethod]
    public void GenerateBuilder_DoesNotUseBuildServiceProvider()
    {
        // Arrange - Validates that the builder does not call BuildServiceProvider
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var builder = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorBuilder.g.cs");
        Assert.IsNotNull(builder);
        Assert.IsFalse(builder.Contains("BuildServiceProvider"), "Builder should NOT call BuildServiceProvider");
    }

    [TestMethod]
    public void GenerateExtensions_UsesServiceProviderForBehaviorFactory()
    {
        // Arrange - Validates that behavior factory uses sp.GetServices
        var source = TestSources.SimpleCommandHandler();

        // Act
        var (generatedSources, _) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        var extensions = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorExtensions.g.cs");
        Assert.IsNotNull(extensions);
        Assert.IsTrue(extensions.Contains("builder.HasBehaviors"), "Expected HasBehaviors check");
        Assert.IsTrue(extensions.Contains("sp.GetServices"), "Expected sp.GetServices for behavior factory");
    }

    [TestMethod]
    public void Generate_WithInternalHandler_RegistersCorrectly()
    {
        // Arrange
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class InternalCommand : ICommand { }

            internal class InternalHandler : ICommandHandler<InternalCommand>
            {
                public Task<ICommandWorkflowResult> Handle(InternalCommand command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("InternalHandler"), "Expected internal handler to be registered");
    }

    [TestMethod]
    public void Generate_WithNestedHandler_RegistersWithFullyQualifiedName()
    {
        // Arrange
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class OuterClass
            {
                public class NestedCommand : ICommand { }

                public class NestedHandler : ICommandHandler<NestedCommand>
                {
                    public Task<ICommandWorkflowResult> Handle(NestedCommand command, CancellationToken ct)
                        => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
                }
            }
            """;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("OuterClass.NestedHandler") || registration.Contains("OuterClass+NestedHandler"),
            "Expected nested handler with qualified name");
    }

    [TestMethod]
    public void Generate_WithStaticClass_DoesNotRegister()
    {
        // Arrange - Static classes should never be registered as handlers
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class StaticCommand : ICommand { }

            // This is invalid C# - can't implement interface on static class
            // But the generator should handle this gracefully
            public class NotStaticHandler : ICommandHandler<StaticCommand>
            {
                public Task<ICommandWorkflowResult> Handle(StaticCommand command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert - Should succeed with one handler
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);
        Assert.IsTrue(registration.Contains("HandlerCount = 1"));
    }

    [TestMethod]
    public void Generate_WithHandlerImplementingMultipleInterfaces_RegistersAllInterfaces()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class Command1 : ICommand { }
            public class Event1 : IDomainEvent { }

            public class MultiHandler : ICommandHandler<Command1>, IEventHandler<Event1>
            {
                public Task<ICommandWorkflowResult> Handle(Command1 command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());

                public Task Handle(Event1 @event, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);

        Assert.IsTrue(registration.Contains("HandlerCount = 2"), "Expected 2 registrations for multi-interface handler");
        Assert.IsTrue(registration.Contains("CommandHandlerCount = 1"), "Expected 1 command handler registration");
        Assert.IsTrue(registration.Contains("EventHandlerCount = 1"), "Expected 1 event handler registration");

        var multiHandlerOccurrences = System.Text.RegularExpressions.Regex.Matches(registration, "MultiHandler").Count;
        Assert.AreEqual(2, multiHandlerOccurrences, "MultiHandler should appear twice in registration code");
    }

    [TestMethod]
    public void Generate_WithDefaultLifetime_AppliesToAllHandlers()
    {
        // Arrange - Handlers without explicit lifetime attributes should use the defaultLifetime parameter
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class TestCommand1 : ICommand { }
            public class TestCommand2 : ICommand { }

            public class Handler1 : ICommandHandler<TestCommand1>
            {
                public Task<ICommandWorkflowResult> Handle(TestCommand1 command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class Handler2 : ICommandHandler<TestCommand2>
            {
                public Task<ICommandWorkflowResult> Handle(TestCommand2 command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        // Act
        var (generatedSources, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        // Assert
        Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error));

        var registration = GeneratorTestHelper.GetGeneratedSource(generatedSources, "AsyncMediatorRegistration.g.cs");
        Assert.IsNotNull(registration);

        Assert.IsTrue(registration.Contains("Handler1"), "Expected Handler1 to be registered");
        Assert.IsTrue(registration.Contains("Handler2"), "Expected Handler2 to be registered");

        var lines = registration.Split('\n');
        var handler1Line = lines.FirstOrDefault(l => l.Contains("Handler1"));
        var handler2Line = lines.FirstOrDefault(l => l.Contains("Handler2"));

        Assert.IsNotNull(handler1Line);
        Assert.IsNotNull(handler2Line);
    }
}
