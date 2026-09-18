using System;
using System.Collections.Generic;

namespace TestProject.Models
{
    /// <summary>
    /// Represents a sample class used for testing the code analyzer functionality.
    /// This class demonstrates various C# language features including properties, methods,
    /// fields, and comprehensive XML documentation.
    /// </summary>
    /// <remarks>
    /// This class is specifically designed to test the documentation parsing capabilities
    /// of the Fanstatic code analyzer. It includes examples of different documentation tags
    /// and various member types.
    /// </remarks>
    /// <example>
    /// var sample = new SampleClass("Test", 42);
    /// sample.ProcessData("input data");
    /// </example>
    public class SampleClass
    {
        /// <summary>
        /// A public field that stores a constant value for testing purposes.
        /// </summary>
        public static readonly string DefaultPrefix = "Sample_";

        /// <summary>
        /// A public field that can be modified externally.
        /// </summary>
        public int PublicField = 100;

        /// <summary>
        /// Gets or sets the name of the sample instance.
        /// </summary>
        /// <value>A string representing the name of this sample instance.</value>
        public string Name { get; set; }

        /// <summary>
        /// Gets the unique identifier for this sample instance.
        /// </summary>
        /// <value>An integer representing the unique ID.</value>
        public int Id { get; private set; }

        /// <summary>
        /// Gets or sets the creation timestamp of this instance.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Gets a computed property that combines the name and ID.
        /// </summary>
        public string DisplayName => $"{DefaultPrefix}{Name}_{Id}";

        /// <summary>
        /// Initializes a new instance of the <see cref="SampleClass"/> class.
        /// </summary>
        /// <param name="name">The name to assign to this instance.</param>
        /// <param name="id">The unique identifier for this instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="id"/> is less than zero.</exception>
        public SampleClass(string name, int id)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Id = id >= 0 ? id : throw new ArgumentException("ID must be non-negative", nameof(id));
        }

        /// <summary>
        /// Processes the provided data and returns a formatted result.
        /// </summary>
        /// <param name="data">The input data to process.</param>
        /// <returns>A formatted string containing the processed data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
        /// <example>
        /// <code>
        /// var result = sample.ProcessData("test input");
        /// Console.WriteLine(result); // Outputs: "Processed: test input"
        /// </code>
        /// </example>
        public string ProcessData(string data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return $"Processed: {data}";
        }

        /// <summary>
        /// Calculates a value based on the provided parameters.
        /// </summary>
        /// <param name="x">The first operand.</param>
        /// <param name="y">The second operand.</param>
        /// <param name="multiplier">An optional multiplier to apply to the result. Defaults to 1.0.</param>
        /// <returns>The calculated result as a double.</returns>
        /// <remarks>
        /// This method performs a simple calculation for demonstration purposes.
        /// The calculation formula is: (x + y) * multiplier
        /// </remarks>
        public double Calculate(int x, int y, double multiplier = 1.0)
        {
            return (x + y) * multiplier;
        }

        /// <summary>
        /// Retrieves a list of sample items for testing purposes.
        /// </summary>
        /// <returns>A list of strings representing sample items.</returns>
        public static List<string> GetSampleItems()
        {
            return new List<string> { "Item1", "Item2", "Item3" };
        }

        /// <summary>
        /// Updates the instance with new data asynchronously.
        /// </summary>
        /// <param name="newName">The new name to set.</param>
        /// <param name="delay">Optional delay in milliseconds before updating.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task<bool> UpdateAsync(string newName, int delay = 0)
        {
            if (delay > 0)
            {
                await Task.Delay(delay);
            }

            Name = newName;
            return true;
        }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            return DisplayName;
        }
    }
}
