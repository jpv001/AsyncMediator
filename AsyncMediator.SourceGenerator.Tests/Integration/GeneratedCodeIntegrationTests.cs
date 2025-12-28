using System.Collections.Immutable;
using System.Reflection;
using AsyncMediator.SourceGenerator.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;

namespace AsyncMediator.SourceGenerator.Tests.Integration;

[TestClass]
public class GeneratedCodeIntegrationTests
{
    [TestMethod]
    public void GeneratedCode_CompilesAndExecutes_FullPipeline()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class TestCommand : ICommand { }

            public class TestCommandHandler : ICommandHandler<TestCommand>
            {
                public static bool WasCalled { get; set; }

                public Task<ICommandWorkflowResult> Handle(TestCommand command, CancellationToken ct)
                {
                    WasCalled = true;
                    return Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
                }
            }
            """;

        var (assembly, handler) = CompileAndLoadAssembly(source);
        var services = new ServiceCollection();

        var addAsyncMediatorMethod = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorServiceCollectionExtensions")!
            .GetMethod("AddAsyncMediator");
        Assert.IsNotNull(addAsyncMediatorMethod, "AddAsyncMediator extension method should exist");

        addAsyncMediatorMethod.Invoke(null, [services, null]);

        var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        Assert.IsNotNull(mediator, "IMediator should be registered");

        var commandType = assembly.GetType("TestNamespace.TestCommand")!;
        var command = Activator.CreateInstance(commandType)!;

        var sendMethod = typeof(IMediator)
            .GetMethod("Send")!
            .MakeGenericMethod(commandType);
        var task = (Task<ICommandWorkflowResult>)sendMethod.Invoke(mediator, [command, CancellationToken.None])!;
        var result = task.Result;
        Assert.IsTrue(result.Success, "Command should execute successfully");

        var wasCalled = (bool)assembly.GetType("TestNamespace.TestCommandHandler")!
            .GetProperty("WasCalled")!
            .GetValue(null)!;
        Assert.IsTrue(wasCalled, "Handler should have been executed");
    }

    [TestMethod]
    public void GeneratedCode_RespectsLifetimes_Singleton()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class TestCommand : ICommand { }

            [MediatorHandler(Lifetime = 0)]
            public class SingletonHandler : ICommandHandler<TestCommand>
            {
                public Task<ICommandWorkflowResult> Handle(TestCommand command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        var (assembly, handler) = CompileAndLoadAssembly(source);
        var services = new ServiceCollection();

        var addAsyncMediatorMethod = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorServiceCollectionExtensions")!
            .GetMethod("AddAsyncMediator");
        addAsyncMediatorMethod!.Invoke(null, [services, null]);

        var commandType = assembly.GetType("TestNamespace.TestCommand");
        Assert.IsNotNull(commandType, "TestCommand type should exist");
        var handlerInterface = typeof(ICommandHandler<>).MakeGenericType(commandType);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == handlerInterface);
        Assert.IsNotNull(descriptor, "Handler should be registered");
        Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime, "Handler should be registered as Singleton");
    }

    [TestMethod]
    public void GeneratedCode_RespectsLifetimes_Scoped()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class TestCommand : ICommand { }

            [MediatorHandler(Lifetime = 1)]
            public class ScopedHandler : ICommandHandler<TestCommand>
            {
                public Task<ICommandWorkflowResult> Handle(TestCommand command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        var (assembly, handler) = CompileAndLoadAssembly(source);
        var services = new ServiceCollection();

        var addAsyncMediatorMethod = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorServiceCollectionExtensions")!
            .GetMethod("AddAsyncMediator");
        addAsyncMediatorMethod!.Invoke(null, [services, null]);

        var commandType = assembly.GetType("TestNamespace.TestCommand");
        Assert.IsNotNull(commandType, "TestCommand type should exist");
        var handlerInterface = typeof(ICommandHandler<>).MakeGenericType(commandType);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == handlerInterface);
        Assert.IsNotNull(descriptor, "Handler should be registered");
        Assert.AreEqual(ServiceLifetime.Scoped, descriptor.Lifetime, "Handler should be registered as Scoped");
    }

    [TestMethod]
    public void GeneratedCode_RespectsLifetimes_Transient()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class TestCommand : ICommand { }

            [MediatorHandler(Lifetime = 2)]
            public class TransientHandler : ICommandHandler<TestCommand>
            {
                public Task<ICommandWorkflowResult> Handle(TestCommand command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        var (assembly, handler) = CompileAndLoadAssembly(source);
        var services = new ServiceCollection();

        var addAsyncMediatorMethod = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorServiceCollectionExtensions")!
            .GetMethod("AddAsyncMediator");
        addAsyncMediatorMethod!.Invoke(null, [services, null]);

        var commandType = assembly.GetType("TestNamespace.TestCommand");
        Assert.IsNotNull(commandType, "TestCommand type should exist");
        var handlerInterface = typeof(ICommandHandler<>).MakeGenericType(commandType);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == handlerInterface);
        Assert.IsNotNull(descriptor, "Handler should be registered");
        Assert.AreEqual(ServiceLifetime.Transient, descriptor.Lifetime, "Handler should be registered as Transient");
    }

    [TestMethod]
    public void GeneratedCode_UsesDefaultLifetime_WhenNoAttributeSpecified()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class TestCommand : ICommand { }

            public class DefaultLifetimeHandler : ICommandHandler<TestCommand>
            {
                public Task<ICommandWorkflowResult> Handle(TestCommand command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        var (assembly, handler) = CompileAndLoadAssembly(source);
        var services = new ServiceCollection();

        var builderType = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorBuilder")!;
        object? builder = null;

        var addAsyncMediatorMethod = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorServiceCollectionExtensions")!
            .GetMethod("AddAsyncMediator");

        var configureAction = new Action<object>(b =>
        {
            builder = b;
            builderType.GetProperty("DefaultLifetime")!.SetValue(b, ServiceLifetime.Singleton);
        });

        var actionType = typeof(Action<>).MakeGenericType(builderType);
        var configureDelegate = Delegate.CreateDelegate(actionType, configureAction.Target, configureAction.Method);

        addAsyncMediatorMethod!.Invoke(null, [services, configureDelegate]);

        var commandType = assembly.GetType("TestNamespace.TestCommand");
        Assert.IsNotNull(commandType, "TestCommand type should exist");
        var handlerInterface = typeof(ICommandHandler<>).MakeGenericType(commandType);

        var descriptor = services.FirstOrDefault(s => s.ServiceType == handlerInterface);
        Assert.IsNotNull(descriptor, "Handler should be registered");
        Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime,
            "Handler without attribute should use builder's DefaultLifetime");
    }

    [TestMethod]
    public void GeneratedCode_RegistersMultipleHandlers()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class Command1 : ICommand { }
            public class Command2 : ICommand { }
            public class Event1 : IDomainEvent { }

            public class CommandHandler1 : ICommandHandler<Command1>
            {
                public Task<ICommandWorkflowResult> Handle(Command1 command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class CommandHandler2 : ICommandHandler<Command2>
            {
                public Task<ICommandWorkflowResult> Handle(Command2 command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }

            public class EventHandler1 : IEventHandler<Event1>
            {
                public Task Handle(Event1 evt, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var (assembly, handler) = CompileAndLoadAssembly(source);
        var services = new ServiceCollection();

        var addAsyncMediatorMethod = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorServiceCollectionExtensions")!
            .GetMethod("AddAsyncMediator");
        addAsyncMediatorMethod!.Invoke(null, [services, null]);

        var provider = services.BuildServiceProvider();

        var command1Type = assembly.GetType("TestNamespace.Command1");
        Assert.IsNotNull(command1Type, "Command1 type should exist");
        var commandHandler1Interface = typeof(ICommandHandler<>).MakeGenericType(command1Type);
        var commandHandler1 = provider.GetService(commandHandler1Interface);
        Assert.IsNotNull(commandHandler1, "CommandHandler1 should be resolvable");

        var command2Type = assembly.GetType("TestNamespace.Command2");
        Assert.IsNotNull(command2Type, "Command2 type should exist");
        var commandHandler2Interface = typeof(ICommandHandler<>).MakeGenericType(command2Type);
        var commandHandler2 = provider.GetService(commandHandler2Interface);
        Assert.IsNotNull(commandHandler2, "CommandHandler2 should be resolvable");

        var event1Type = assembly.GetType("TestNamespace.Event1");
        Assert.IsNotNull(event1Type, "Event1 type should exist");
        var eventHandler1Interface = typeof(IEventHandler<>).MakeGenericType(event1Type);
        var eventHandler1 = provider.GetService(eventHandler1Interface);
        Assert.IsNotNull(eventHandler1, "EventHandler1 should be resolvable");
    }

    [TestMethod]
    public void GeneratedCode_ExcludesHandlerWithAttribute()
    {
        var source = """
            using AsyncMediator;
            using System.Threading;
            using System.Threading.Tasks;

            namespace TestNamespace;

            public class TestCommand : ICommand { }

            [ExcludeFromMediator]
            public class ExcludedHandler : ICommandHandler<TestCommand>
            {
                public Task<ICommandWorkflowResult> Handle(TestCommand command, CancellationToken ct)
                    => Task.FromResult<ICommandWorkflowResult>(CommandWorkflowResult.Ok());
            }
            """;

        var (assembly, handler) = CompileAndLoadAssembly(source);
        var services = new ServiceCollection();

        var addAsyncMediatorMethod = assembly.GetType("Microsoft.Extensions.DependencyInjection.AsyncMediatorServiceCollectionExtensions")!
            .GetMethod("AddAsyncMediator");
        addAsyncMediatorMethod!.Invoke(null, [services, null]);

        var provider = services.BuildServiceProvider();

        var commandType = assembly.GetType("TestNamespace.TestCommand");
        Assert.IsNotNull(commandType, "TestCommand type should exist");
        var handlerInterface = typeof(ICommandHandler<>).MakeGenericType(commandType);
        var excludedHandler = provider.GetService(handlerInterface);
        Assert.IsNull(excludedHandler, "Excluded handler should not be registered");
    }

    private static (Assembly Assembly, Type? HandlerType) CompileAndLoadAssembly(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = GetRequiredReferences();

        var compilation = CSharpCompilation.Create(
            $"TestAssembly_{Guid.NewGuid():N}",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new HandlerDiscoveryGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count != 0)
        {
            var errorMessages = string.Join(Environment.NewLine, errors.Select(e => e.GetMessage()));
            Assert.Fail($"Compilation failed with errors:{Environment.NewLine}{errorMessages}");
        }

        using var ms = new MemoryStream();
        var emitResult = outputCompilation.Emit(ms);

        if (!emitResult.Success)
        {
            var failures = emitResult.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
            var errorMessages = string.Join(Environment.NewLine, failures.Select(e => e.GetMessage()));
            Assert.Fail($"Emit failed with errors:{Environment.NewLine}{errorMessages}");
        }

        ms.Seek(0, SeekOrigin.Begin);
        var assembly = Assembly.Load(ms.ToArray());

        return (assembly, null);
    }

    private static List<MetadataReference> GetRequiredReferences()
    {
        var assemblies = new[]
        {
            typeof(object).Assembly,
            typeof(Console).Assembly,
            typeof(Task).Assembly,
            typeof(ICommand).Assembly,
            typeof(ServiceCollection).Assembly,
            typeof(IServiceCollection).Assembly,
            typeof(IServiceProvider).Assembly,
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("System.Linq"),
            Assembly.Load("System.ComponentModel"),
            Assembly.Load("netstandard"),
            Assembly.Load("Microsoft.Extensions.DependencyInjection.Abstractions"),
        };

        return assemblies
            .Where(a => !string.IsNullOrEmpty(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .ToList();
    }
}
