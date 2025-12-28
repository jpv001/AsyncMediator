using AsyncMediator.Tests.Fakes;
using AsyncMediator.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel.DataAnnotations;

namespace AsyncMediator.Tests;

[TestClass]
public class CriticalBugFixTests
{
    [TestMethod]
    public async Task BehaviorRegistration_ViaServiceCollection_ResolvesCorrectly()
    {
        var services = new ServiceCollection();
        var executionOrder = new List<string>();

        services.AddSingleton(executionOrder);
        services.AddTransient(typeof(IPipelineBehavior<TestCommand, ICommandWorkflowResult>), typeof(TrackingBehavior));

        services.AddSingleton<SingleInstanceFactory>(sp => type => sp.GetRequiredService(type));
        services.AddSingleton<MultiInstanceFactory>(sp => type => sp.GetServices(type).Cast<object>());

        services.AddScoped(typeof(IMediator), sp => new Mediator(
            sp.GetRequiredService<MultiInstanceFactory>(),
            sp.GetRequiredService<SingleInstanceFactory>(),
            behaviorFactory: type => sp.GetServices(type).Where(x => x != null).Cast<object>()));

        services.AddScoped<ICommandHandler<TestCommand>>(sp =>
            new TestCommandHandler(sp.GetRequiredService<IMediator>()));
        services.AddScoped<IEventHandler<FakeEvent>>(sp => new FakeEventHandler());
        services.AddScoped<IEventHandler<FakeEventFromHandler>>(sp => new FakeEventFromHandlerHandler());

        var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var result = await mediator.Send(new TestCommand { Id = 1 });

        Assert.IsTrue(result.Success);
        Assert.AreEqual(2, executionOrder.Count, "Behavior should have executed");
        Assert.AreEqual("TrackingBehavior-Before", executionOrder[0]);
        Assert.AreEqual("TrackingBehavior-After", executionOrder[1]);
    }

    [TestMethod]
    public async Task BehaviorCache_WithSameRequestDifferentResponse_CachesIndependently()
    {
        var executionOrder = new List<string>();
        var registry = new TestHandlerRegistry();

        var stringBehavior = new TypedResponseBehavior<SharedRequest, string>(executionOrder, "StringBehavior");
        var intBehavior = new TypedResponseBehavior<SharedRequest, int>(executionOrder, "IntBehavior");

        BehaviorFactory factory = type =>
        {
            if (type == typeof(IPipelineBehavior<SharedRequest, string>))
                return new object[] { stringBehavior };
            if (type == typeof(IPipelineBehavior<SharedRequest, int>))
                return new object[] { intBehavior };
            return [];
        };

        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviorFactory: factory);

        registry.AddHandlersForCommandOrQuery<IQuery<SharedRequest, string>>(new StringQueryHandler());
        registry.AddHandlersForCommandOrQuery<IQuery<SharedRequest, int>>(new IntQueryHandler());

        var stringResult = await mediator.Query<SharedRequest, string>(new SharedRequest());
        var intResult = await mediator.Query<SharedRequest, int>(new SharedRequest());

        Assert.AreEqual("result", stringResult);
        Assert.AreEqual(42, intResult);

        Assert.IsTrue(executionOrder.Contains("StringBehavior-Before"), "String behavior should execute for string query");
        Assert.IsTrue(executionOrder.Contains("StringBehavior-After"), "String behavior should execute for string query");
        Assert.IsTrue(executionOrder.Contains("IntBehavior-Before"), "Int behavior should execute for int query");
        Assert.IsTrue(executionOrder.Contains("IntBehavior-After"), "Int behavior should execute for int query");

        Assert.IsFalse(executionOrder.Any(s => s.Contains("StringBehavior") && executionOrder.IndexOf(s) > executionOrder.IndexOf("IntBehavior-Before")),
            "String behavior should not execute for int query");
    }

    [TestMethod]
    public void ValidationResults_DirectMutation_UpdatesSuccessProperty()
    {
        var result = CommandWorkflowResult.Ok();
        Assert.IsTrue(result.Success);

        result.ValidationResults.Add(new ValidationResult("error"));

        Assert.IsFalse(result.Success, "Success should reflect the mutated list");
        Assert.AreEqual(1, result.ValidationResults.Count);
    }

    [TestMethod]
    public void ValidationResults_DirectMutation_AfterCreation_IsVisible()
    {
        var result = new CommandWorkflowResult();
        Assert.IsTrue(result.Success);
        Assert.AreEqual(0, result.ValidationResults.Count);

        result.ValidationResults.Add(new ValidationResult("error 1"));
        result.ValidationResults.Add(new ValidationResult("error 2"));

        Assert.IsFalse(result.Success);
        Assert.AreEqual(2, result.ValidationResults.Count);
    }

    [TestMethod]
    public void ValidationResults_MixedMutation_AddErrorThenDirect_BothVisible()
    {
        var result = new CommandWorkflowResult();
        result.AddError("error 1");

        Assert.AreEqual(1, result.ValidationResults.Count);

        result.ValidationResults.Add(new ValidationResult("error 2"));

        Assert.AreEqual(2, result.ValidationResults.Count);
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public void ValidationResults_DirectMutation_ThenAddError_AllVisible()
    {
        var result = new CommandWorkflowResult();

        result.ValidationResults.Add(new ValidationResult("error 1"));
        Assert.AreEqual(1, result.ValidationResults.Count);

        result.AddError("error 2");

        Assert.AreEqual(2, result.ValidationResults.Count);
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public void ValidationResults_MultipleAccessors_ReturnSameList()
    {
        var result = new CommandWorkflowResult();

        var list1 = result.ValidationResults;
        list1.Add(new ValidationResult("error 1"));

        var list2 = result.ValidationResults;
        list2.Add(new ValidationResult("error 2"));

        Assert.AreSame(list1, list2, "Multiple accesses should return the same list instance");
        Assert.AreEqual(2, result.ValidationResults.Count);
        Assert.IsFalse(result.Success);
    }

    [TestMethod]
    public async Task ExplicitBehaviors_WithSameRequestDifferentResponse_CorrectlyIsolated()
    {
        // Explicit behaviors are keyed by (Request, Response) tuple to isolate behaviors
        // when the same request type has different response types.
        var executionOrder = new List<string>();
        var registry = new TestHandlerRegistry();

        var stringBehavior = new TypedResponseBehavior<SharedRequest, string>(executionOrder, "ExplicitStringBehavior");
        var intBehavior = new TypedResponseBehavior<SharedRequest, int>(executionOrder, "ExplicitIntBehavior");

        var mediator = new Mediator(
            registry.MultiInstanceFactory,
            registry.SingleInstanceFactory,
            behaviors: [stringBehavior, intBehavior],
            behaviorFactory: null);

        registry.AddHandlersForCommandOrQuery<IQuery<SharedRequest, string>>(new StringQueryHandler());
        registry.AddHandlersForCommandOrQuery<IQuery<SharedRequest, int>>(new IntQueryHandler());

        executionOrder.Clear();
        var stringResult = await mediator.Query<SharedRequest, string>(new SharedRequest());
        Assert.AreEqual("result", stringResult);
        Assert.IsTrue(executionOrder.Contains("ExplicitStringBehavior-Before"), "String behavior should execute");
        Assert.IsTrue(executionOrder.Contains("ExplicitStringBehavior-After"), "String behavior should execute");
        Assert.IsFalse(executionOrder.Contains("ExplicitIntBehavior-Before"), "Int behavior should NOT execute for string query");

        executionOrder.Clear();
        var intResult = await mediator.Query<SharedRequest, int>(new SharedRequest());
        Assert.AreEqual(42, intResult);
        Assert.IsTrue(executionOrder.Contains("ExplicitIntBehavior-Before"), "Int behavior should execute");
        Assert.IsTrue(executionOrder.Contains("ExplicitIntBehavior-After"), "Int behavior should execute");
        Assert.IsFalse(executionOrder.Contains("ExplicitStringBehavior-Before"), "String behavior should NOT execute for int query");
    }
}

file sealed class TrackingBehavior : IPipelineBehavior<TestCommand, ICommandWorkflowResult>
{
    private readonly List<string> _executionOrder;

    public TrackingBehavior(List<string> executionOrder)
    {
        _executionOrder = executionOrder;
    }

    public async Task<ICommandWorkflowResult> Handle(
        TestCommand request,
        RequestHandlerDelegate<ICommandWorkflowResult> next,
        CancellationToken cancellationToken)
    {
        _executionOrder.Add("TrackingBehavior-Before");
        var response = await next();
        _executionOrder.Add("TrackingBehavior-After");
        return response;
    }
}

file sealed class TypedResponseBehavior<TRequest, TResponse>(List<string> executionOrder, string name)
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        executionOrder.Add($"{name}-Before");
        var response = await next();
        executionOrder.Add($"{name}-After");
        return response;
    }
}

file sealed class SharedRequest { }

file sealed class StringQueryHandler : IQuery<SharedRequest, string>
{
    public Task<string> Query(SharedRequest criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult("result");
}

file sealed class IntQueryHandler : IQuery<SharedRequest, int>
{
    public Task<int> Query(SharedRequest criteria, CancellationToken cancellationToken = default) =>
        Task.FromResult(42);
}

file sealed class FakeEventHandler : IEventHandler<FakeEvent>
{
    public Task Handle(FakeEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

file sealed class FakeEventFromHandlerHandler : IEventHandler<FakeEventFromHandler>
{
    public Task Handle(FakeEventFromHandler @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
