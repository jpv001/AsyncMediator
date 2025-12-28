using Microsoft.CodeAnalysis;

namespace AsyncMediator.SourceGenerator.Analyzers;

internal static class DiagnosticDescriptors
{
    private const string Category = "AsyncMediator";

    public static readonly DiagnosticDescriptor MissingCommandHandler = new(
        id: "ASYNCMED001",
        title: "Missing command handler",
        messageFormat: "No handler registered for command '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor DuplicateCommandHandler = new(
        id: "ASYNCMED002",
        title: "Duplicate command handler",
        messageFormat: "Multiple handlers registered for command '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor MissingQueryHandler = new(
        id: "ASYNCMED003",
        title: "Missing query handler",
        messageFormat: "No handler registered for query '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor MissingEventHandler = new(
        id: "ASYNCMED004",
        title: "No event handlers registered",
        messageFormat: "No handlers registered for event '{0}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor UnusedHandler = new(
        id: "ASYNCMED005",
        title: "Unused handler",
        messageFormat: "Handler '{0}' is registered but never used",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DraftMessage = new(
        id: "ASYNCMED006",
        title: "Draft message",
        messageFormat: "Message '{0}' is marked as draft{1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static readonly DiagnosticDescriptor DuplicateQueryHandler = new(
        id: "ASYNCMED007",
        title: "Duplicate query handler",
        messageFormat: "Multiple handlers registered for query '{0}': {1}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);
}
