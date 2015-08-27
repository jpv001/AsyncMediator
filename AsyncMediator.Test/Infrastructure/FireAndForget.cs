using System.Threading.Tasks;

namespace AsyncMediator.Test
{
    /// <summary>
    /// This extension should only be used for testing - it allows for fire-and-forget tasks to be used
    /// Removes the need for #pragma warning disable 4014 wrapping
    /// </summary>
    internal static class TestingTaskExtension
    {
        internal static void FireAndForget(this Task task)
        {
        }
    }
}
