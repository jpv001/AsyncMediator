namespace AsyncMediator.Tests.Fakes;

public sealed class TestCommand : ICommand
{
    public int Id { get; set; }
}

public sealed class TestCommandWithResult : ICommand
{
    public int Id { get; set; }
}

public sealed class TestMultipleCommandWithResult : ICommand
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class CommandMissing : ICommand
{
    public int Id { get; set; }
}

public sealed class TestCommandResult
{
    public int ResultingValue { get; set; }
}
