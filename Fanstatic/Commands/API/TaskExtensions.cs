namespace Fanstatic.Commands.API;

/// <summary>
/// Helper extension for Task.WhenAll with IEnumerable
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Creates a task that completes when all of the provided tasks have completed.
    /// </summary>
    /// <param name="tasks">An array of tasks to wait on for completion.</param>
    /// <returns>A task that represents the completion of all the provided tasks.</returns>
    public static Task WhenAll(this Task[] tasks) => Task.WhenAll(tasks);
}
