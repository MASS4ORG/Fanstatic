using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TestProject.Models;

namespace TestProject.Services
{
    /// <summary>
    /// Defines the contract for data service operations within the application.
    /// This interface provides methods for managing and retrieving data entities
    /// and is used to test the code analyzer's interface parsing capabilities.
    /// </summary>
    /// <remarks>
    /// This interface demonstrates various method signatures including
    /// synchronous and asynchronous operations, generic methods, and
    /// different parameter configurations.
    /// </remarks>
    public interface IDataService
    {
        /// <summary>
        /// Retrieves a user by their unique identifier.
        /// </summary>
        /// <param name="userId">The unique identifier of the user to retrieve.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the user if found, or null if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when userId is less than or equal to zero.</exception>
        Task<UserRecord?> GetUserAsync(int userId);

        /// <summary>
        /// Retrieves all users that match the specified criteria.
        /// </summary>
        /// <param name="filter">An optional filter function to apply to the user collection.</param>
        /// <param name="maxResults">The maximum number of results to return. Defaults to 100.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of matching users.</returns>
        Task<IEnumerable<UserRecord>> GetUsersAsync(Func<UserRecord, bool>? filter = null, int maxResults = 100);

        /// <summary>
        /// Creates a new user in the system.
        /// </summary>
        /// <param name="user">The user record to create.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the created user with assigned ID.</returns>
        /// <exception cref="ArgumentNullException">Thrown when user is null.</exception>
        /// <exception cref="ArgumentException">Thrown when user data is invalid.</exception>
        Task<UserRecord> CreateUserAsync(UserRecord user);

        /// <summary>
        /// Updates an existing user's information.
        /// </summary>
        /// <param name="userId">The ID of the user to update.</param>
        /// <param name="updatedUser">The updated user information.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the update was successful.</returns>
        Task<bool> UpdateUserAsync(int userId, UserRecord updatedUser);

        /// <summary>
        /// Deletes a user from the system.
        /// </summary>
        /// <param name="userId">The ID of the user to delete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the deletion was successful.</returns>
        Task<bool> DeleteUserAsync(int userId);

        /// <summary>
        /// Retrieves statistics about the data in the system.
        /// </summary>
        /// <returns>A dictionary containing various statistics as key-value pairs.</returns>
        Dictionary<string, object> GetStatistics();

        /// <summary>
        /// Processes data entities based on the specified status.
        /// </summary>
        /// <param name="status">The status filter to apply during processing.</param>
        /// <param name="batchSize">The number of entities to process in each batch.</param>
        /// <returns>The number of entities that were successfully processed.</returns>
        int ProcessEntitiesByStatus(SampleStatus status, int batchSize = 50);

        /// <summary>
        /// Validates the integrity of the data store.
        /// </summary>
        /// <param name="performDeepCheck">Whether to perform a comprehensive validation check.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains validation results.</returns>
        Task<ValidationResult> ValidateDataIntegrityAsync(bool performDeepCheck = false);
    }

    /// <summary>
    /// Represents the result of a data validation operation.
    /// </summary>
    /// <param name="IsValid">Indicates whether the validation passed.</param>
    /// <param name="ErrorCount">The number of errors found during validation.</param>
    /// <param name="WarningCount">The number of warnings found during validation.</param>
    /// <param name="Details">Additional details about the validation results.</param>
    public record ValidationResult(
        bool IsValid,
        int ErrorCount,
        int WarningCount,
        List<string> Details
    );
}