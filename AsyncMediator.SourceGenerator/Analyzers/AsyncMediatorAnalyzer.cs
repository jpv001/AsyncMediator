using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AsyncMediator.SourceGenerator.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsyncMediatorAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        DiagnosticDescriptors.MissingCommandHandler,
        DiagnosticDescriptors.DuplicateCommandHandler,
        DiagnosticDescriptors.MissingQueryHandler,
        DiagnosticDescriptors.MissingEventHandler,
        DiagnosticDescriptors.UnusedHandler,
        DiagnosticDescriptors.DraftMessage,
        DiagnosticDescriptors.DuplicateQueryHandler
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnCompilationStart);
    }

    private static void OnCompilationStart(CompilationStartAnalysisContext context)
    {
        var state = new AnalysisState();

        context.RegisterSymbolAction(ctx => AnalyzeType((INamedTypeSymbol)ctx.Symbol, state), SymbolKind.NamedType);
        context.RegisterSyntaxNodeAction(ctx => AnalyzeInvocation(ctx, state), SyntaxKind.InvocationExpression);
        context.RegisterCompilationEndAction(ctx => ReportDiagnostics(ctx, state));
    }

    private static void AnalyzeType(INamedTypeSymbol symbol, AnalysisState state)
    {
        if (symbol.IsAbstract || symbol.TypeKind is not (TypeKind.Class or TypeKind.Struct))
            return;

        var draftAttr = symbol.GetAttributes().FirstOrDefault(a =>
        {
            if (a.AttributeClass is null) return false;
            var ns = a.AttributeClass.ContainingNamespace?.ToDisplayString();
            if (ns != "AsyncMediator") return false;
            return a.AttributeClass.Name is "MediatorDraftAttribute" or "MediatorDraft";
        });

        if (draftAttr is not null)
        {
            var reason = draftAttr.NamedArguments.FirstOrDefault(a => a.Key == "Reason").Value.Value as string;
            state.DraftMessages[symbol.ToDisplayString()] = (symbol.Locations.FirstOrDefault() ?? Location.None, reason);
        }

        foreach (var iface in symbol.AllInterfaces)
        {
            var ns = iface.ContainingNamespace?.ToDisplayString();
            if (ns is not ("AsyncMediator" or "AsyncMediator.Interfaces"))
                continue;

            var location = symbol.Locations.FirstOrDefault() ?? Location.None;

            if (iface.Name == "ICommandHandler" && iface.TypeArguments.Length >= 1)
            {
                var commandType = iface.TypeArguments[0].ToDisplayString();
                state.CommandHandlers.AddOrUpdate(commandType,
                    _ => ImmutableArray.Create((symbol, location)),
                    (_, existing) => existing.Add((symbol, location)));
            }
            else if (iface.Name == "IEventHandler" && iface.TypeArguments.Length >= 1)
            {
                var eventType = iface.TypeArguments[0].ToDisplayString();
                state.EventHandlers.AddOrUpdate(eventType,
                    _ => ImmutableArray.Create((symbol, location)),
                    (_, existing) => existing.Add((symbol, location)));
            }
            else if (iface.Name == "IQuery" && iface.TypeArguments.Length >= 2)
            {
                // Key by query signature: IQuery<TCriteria, TResult>
                var queryKey = $"IQuery<{iface.TypeArguments[0].ToDisplayString()}, {iface.TypeArguments[1].ToDisplayString()}>";
                state.QueryHandlers.AddOrUpdate(queryKey,
                    _ => ImmutableArray.Create((symbol, location)),
                    (_, existing) => existing.Add((symbol, location)));
            }
            else if (iface.Name == "ILookupQuery" && iface.TypeArguments.Length >= 1)
            {
                // Key by lookup signature: ILookupQuery<TResult>
                var queryKey = $"ILookupQuery<{iface.TypeArguments[0].ToDisplayString()}>";
                state.QueryHandlers.AddOrUpdate(queryKey,
                    _ => ImmutableArray.Create((symbol, location)),
                    (_, existing) => existing.Add((symbol, location)));
            }
        }
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, AnalysisState state)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var methodName })
            return;

        if (methodName is not ("Send" or "Publish" or "Query"))
            return;

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol { ContainingType.Name: "IMediator" or "Mediator" })
            return;

        if (invocation.ArgumentList.Arguments.Count == 0)
            return;

        var firstArg = invocation.ArgumentList.Arguments[0].Expression;
        if (context.SemanticModel.GetTypeInfo(firstArg).Type is not INamedTypeSymbol messageType)
            return;

        var category = methodName switch
        {
            "Send" => MessageCategory.Command,
            "Publish" => MessageCategory.Event,
            "Query" => MessageCategory.Query,
            _ => MessageCategory.Unknown
        };

        state.UsedMessages.Add((messageType.ToDisplayString(), invocation.GetLocation(), category));
    }

    private static void ReportDiagnostics(CompilationAnalysisContext context, AnalysisState state)
    {
        foreach (var kvp in state.DraftMessages)
        {
            var reasonText = string.IsNullOrEmpty(kvp.Value.Reason) ? "" : $" - {kvp.Value.Reason}";
            context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DraftMessage, kvp.Value.Location, kvp.Key, reasonText));
        }

        foreach (var (typeName, location, category) in state.UsedMessages)
        {
            if (state.DraftMessages.ContainsKey(typeName))
                continue;

            switch (category)
            {
                case MessageCategory.Command when !state.CommandHandlers.ContainsKey(typeName):
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MissingCommandHandler, location, typeName));
                    break;
                case MessageCategory.Event when !state.EventHandlers.ContainsKey(typeName):
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MissingEventHandler, location, typeName));
                    break;
                case MessageCategory.Query when !state.QueryHandlers.ContainsKey(typeName):
                    context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.MissingQueryHandler, location, typeName));
                    break;
            }
        }

        foreach (var kvp in state.CommandHandlers.Where(kvp => kvp.Value.Length > 1))
        {
            var handlerNames = string.Join(", ", kvp.Value.Select(h => h.Handler.Name));
            foreach (var (handler, location) in kvp.Value)
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DuplicateCommandHandler, location, kvp.Key, handlerNames));
        }

        foreach (var kvp in state.QueryHandlers.Where(kvp => kvp.Value.Length > 1))
        {
            var handlerNames = string.Join(", ", kvp.Value.Select(h => h.Handler.Name));
            foreach (var (handler, location) in kvp.Value)
                context.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.DuplicateQueryHandler, location, kvp.Key, handlerNames));
        }
    }

    private sealed class AnalysisState
    {
        public ConcurrentDictionary<string, ImmutableArray<(INamedTypeSymbol Handler, Location Location)>> CommandHandlers { get; } = new();
        public ConcurrentDictionary<string, ImmutableArray<(INamedTypeSymbol Handler, Location Location)>> EventHandlers { get; } = new();
        public ConcurrentDictionary<string, ImmutableArray<(INamedTypeSymbol Handler, Location Location)>> QueryHandlers { get; } = new();
        public ConcurrentDictionary<string, (Location Location, string? Reason)> DraftMessages { get; } = new();
        public ConcurrentBag<(string TypeName, Location Location, MessageCategory Category)> UsedMessages { get; } = [];
    }

    private enum MessageCategory { Unknown, Command, Event, Query }
}
