namespace AsyncMediator.Tests.Infrastructure;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        // Used in tests to suppress warning about not awaiting returned Task
        // The NSubstitute framework handles verifying the call was made
    }
}
