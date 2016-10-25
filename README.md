# AsyncMediator

An async implementation of the Mediator pattern.

Contains a set of classes used for commands, events and mapping, and the interface and implementation of the (I)Mediator. This is a trimmed down version of Jimmy Bogard's https://github.com/jbogard/MediatR

***Installing AsyncMediator***

You should use NuGet to install AsyncMediator into your solution: https://www.nuget.org/packages/AsyncMediator/

`Install-Package AsyncMediator`

The implementation is independent of any dependency injection framework, but can be wired up to use any DI/IoC framework, e.g. Autofac, StructureMap, Ninject etc.

To do this, you should register the MultiInstanceFactory and SingleInstanceFactory delegates, your event handlers, command handlers and queries, and the Mediator.

Then, anything that has a dependency on the IMediator will get a fully functional instance of the Mediator.

Example (Autofac):

    public class MediatorModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // MultiInstanceFactory is a delegate that gets a Type and returns list of objects of that type.
            // It is used when instantiating event handlers in the Mediator. Here, we register MultiInstanceFactory
            // that will be later passed to Mediator. Mediator will then use this delegate as a factory to build 
            // handlers.

            builder.Register<MultiInstanceFactory>(ctx =>
            {
                var c = ctx.Resolve<IComponentContext>();
                return type => (IEnumerable<object>)c.Resolve(typeof(IEnumerable<>).MakeGenericType(type));
            });

            // SingleInstanceFactory is similar to MultiInstanceFactory but we use it to create a single instance 
            // of the given type. It is used when instantiating command handlers in the Mediator. Again, here we 
            // define a SingleInstanceFactory delegate that will be later passed in the Mediator. Mediator will use
            // this delegate as a factory to build command handlers.

            builder.Register<SingleInstanceFactory>(ctx =>
            {
                var c = ctx.Resolve<IComponentContext>();
                return type => c.Resolve(type);
            });

            // The Mediator should be registered as it's IMediator interface, so any class that has a dependency on
            // IMediator will get a reference to this class.  Assuming this is a web application, you can use
            // InstancePerRequest to get the same instance of the (I)Mediator for a single Web/API request.

            builder.RegisterType<Mediator>().As<IMediator>().InstancePerRequest();

            // All your (I)EventHandlers, (I)CommandHandlers and (I)Query types should be registered in the IoC
            // container, so that the SingleInstanceFactory and MultiInstanceFactory delegates will resolve them.

            builder.RegisterAssemblyTypes(ThisAssembly).AsClosedTypesOf(typeof(IEventHandler<>));

            builder.RegisterAssemblyTypes(ThisAssembly).AsClosedTypesOf(typeof(ICommandHandler<>));

            builder.RegisterAssemblyTypes(ThisAssembly).AsClosedTypesOf(typeof(IQuery<,>));

            builder.RegisterAssemblyTypes(ThisAssembly).AsClosedTypesOf(typeof(ILookupQuery<>));
        }
    }
