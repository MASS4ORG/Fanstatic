using System;
using System.Collections.Generic;

namespace TestProject.Models
{
    /// <summary>
    /// Represents a data transfer object for user information using C# record syntax.
    /// This record is designed to test the code analyzer's ability to parse record declarations
    /// and their associated properties and documentation.
    /// </summary>
    /// <param name="Id">The unique identifier for the user.</param>
    /// <param name="FirstName">The user's first name.</param>
    /// <param name="LastName">The user's last name.</param>
    /// <param name="Email">The user's email address.</param>
    /// <param name="DateOfBirth">The user's date of birth.</param>
    /// <remarks>
    /// Records provide value-based equality semantics and are immutable by default,
    /// making them ideal for data transfer objects and value types.
    /// </remarks>
    /// <example>
    /// var user = new UserRecord(1, "John", "Doe", "john.doe@example.com", new DateTime(1990, 1, 1));
    /// var updatedUser = user with { Email = "john.newemail@example.com" };
    /// </example>
    public record UserRecord(
        int Id,
        string FirstName,
        string LastName,
        string Email,
        DateTime DateOfBirth)
    {
        /// <summary>
        /// Gets the full name by combining first and last names.
        /// </summary>
        /// <value>A string containing the user's full name.</value>
        public string FullName => $"{FirstName} {LastName}";

        /// <summary>
        /// Gets the user's age calculated from their date of birth.
        /// </summary>
        /// <value>An integer representing the user's age in years.</value>
        public int Age => DateTime.Now.Year - DateOfBirth.Year - 
            (DateTime.Now.DayOfYear < DateOfBirth.DayOfYear ? 1 : 0);

        /// <summary>
        /// Gets or sets additional metadata associated with the user.
        /// </summary>
        /// <value>A dictionary containing key-value pairs of metadata.</value>
        public Dictionary<string, object> Metadata { get; init; } = new();

        /// <summary>
        /// Validates whether the user record contains valid data.
        /// </summary>
        /// <returns>True if the user data is valid; otherwise, false.</returns>
        /// <remarks>
        /// This method checks basic validation rules such as non-empty names
        /// and valid email format patterns.
        /// </remarks>
        public bool IsValid()
        {
            return !string.IsNullOrWhiteSpace(FirstName) &&
                   !string.IsNullOrWhiteSpace(LastName) &&
                   !string.IsNullOrWhiteSpace(Email) &&
                   Email.Contains("@") &&
                   DateOfBirth <= DateTime.Now;
        }

        /// <summary>
        /// Creates a display-friendly representation of the user information.
        /// </summary>
        /// <param name="includeEmail">Whether to include the email address in the display.</param>
        /// <returns>A formatted string representation of the user.</returns>
        public string ToDisplayString(bool includeEmail = true)
        {
            var display = $"{FullName} (Age: {Age})";
            return includeEmail ? $"{display} - {Email}" : display;
        }
    }

    /// <summary>
    /// Represents a simple data structure for coordinate information.
    /// </summary>
    /// <param name="X">The X coordinate value.</param>
    /// <param name="Y">The Y coordinate value.</param>
    public record struct Point(double X, double Y)
    {
        /// <summary>
        /// Calculates the distance from the origin (0,0) to this point.
        /// </summary>
        /// <returns>The distance as a double value.</returns>
        public readonly double DistanceFromOrigin() => Math.Sqrt(X * X + Y * Y);

        /// <summary>
        /// Calculates the distance between this point and another point.
        /// </summary>
        /// <param name="other">The other point to calculate distance to.</param>
        /// <returns>The distance between the two points.</returns>
        public readonly double DistanceTo(Point other)
        {
            var dx = X - other.X;
            var dy = Y - other.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}