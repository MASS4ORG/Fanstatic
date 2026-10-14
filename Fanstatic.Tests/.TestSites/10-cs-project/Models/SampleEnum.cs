namespace TestProject.Models
{
    /// <summary>
    /// Represents different status values that can be assigned to various entities in the system.
    /// This enum is used for testing the code analyzer's ability to parse enum declarations
    /// and their associated documentation.
    /// </summary>
    /// <remarks>
    /// This enumeration provides a set of predefined status values that help categorize
    /// the current state of objects within the application.
    /// </remarks>
    public enum SampleStatus
    {
        /// <summary>
        /// Indicates that the entity has not been initialized or is in an unknown state.
        /// </summary>
        None = 0,

        /// <summary>
        /// Indicates that the entity is currently being processed or is in progress.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Indicates that the entity is currently active and operational.
        /// </summary>
        Active = 2,

        /// <summary>
        /// Indicates that the entity has been temporarily suspended or paused.
        /// </summary>
        Suspended = 3,

        /// <summary>
        /// Indicates that the entity has completed its lifecycle successfully.
        /// </summary>
        Completed = 100,

        /// <summary>
        /// Indicates that the entity has failed or encountered an error.
        /// </summary>
        Failed = 999
    }

    /// <summary>
    /// Defines priority levels for task or request handling.
    /// </summary>
    public enum Priority
    {
        /// <summary>
        /// Low priority - can be processed when time permits.
        /// </summary>
        Low,

        /// <summary>
        /// Normal priority - standard processing order.
        /// </summary>
        Normal,

        /// <summary>
        /// High priority - should be processed before normal items.
        /// </summary>
        High,

        /// <summary>
        /// Critical priority - requires immediate attention.
        /// </summary>
        Critical
    }
}