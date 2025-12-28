namespace AsyncMediator.SourceGenerator.Models;

internal readonly record struct HandlerInfo(
    string FullTypeName,
    string ServiceInterfaceFullName,
    HandlerCategory Category,
    string? Lifetime = null);

internal enum HandlerCategory
{
    Command,
    Event,
    Query,
    LookupQuery
}
