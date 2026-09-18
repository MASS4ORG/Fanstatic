using System;

namespace TestProject.Models
{
    /// <summary>
    /// Represents a lightweight data structure for storing dimensional information.
    /// This struct is designed to test the code analyzer's ability to parse struct declarations
    /// and their associated properties, fields, and documentation.
    /// </summary>
    /// <remarks>
    /// Structs are value types that are typically used for small data structures
    /// that have value semantics. This example demonstrates various features
    /// including constructors, properties, and methods.
    /// </remarks>
    /// <example>
    /// var dimensions = new Dimensions(1920, 1080);
    /// var area = dimensions.CalculateArea();
    /// </example>
    public struct Dimensions
    {
        /// <summary>
        /// A constant representing the maximum allowed width value.
        /// </summary>
        public const int MaxWidth = 7680;

        /// <summary>
        /// A constant representing the maximum allowed height value.
        /// </summary>
        public const int MaxHeight = 4320;

        /// <summary>
        /// A public field that indicates whether the dimensions have been initialized.
        /// </summary>
        public bool IsInitialized;

        /// <summary>
        /// Gets or sets the width value.
        /// </summary>
        /// <value>An integer representing the width in pixels.</value>
        public int Width { get; set; }

        /// <summary>
        /// Gets or sets the height value.
        /// </summary>
        /// <value>An integer representing the height in pixels.</value>
        public int Height { get; set; }

        /// <summary>
        /// Gets the aspect ratio of the dimensions.
        /// </summary>
        /// <value>A double representing the width-to-height ratio.</value>
        public readonly double AspectRatio => Height == 0 ? 0 : (double)Width / Height;

        /// <summary>
        /// Gets a value indicating whether the dimensions represent a square.
        /// </summary>
        /// <value>True if width equals height; otherwise, false.</value>
        public readonly bool IsSquare => Width == Height && Width > 0;

        /// <summary>
        /// Initializes a new instance of the <see cref="Dimensions"/> struct.
        /// </summary>
        /// <param name="width">The width value to set.</param>
        /// <param name="height">The height value to set.</param>
        /// <exception cref="ArgumentException">Thrown when width or height is negative.</exception>
        public Dimensions(int width, int height)
        {
            if (width < 0)
            {
                throw new ArgumentException("Width cannot be negative", nameof(width));
            }
            if (height < 0)
            {
                throw new ArgumentException("Height cannot be negative", nameof(height));
            }

            Width = width;
            Height = height;
            IsInitialized = true;
        }

        /// <summary>
        /// Calculates the total area represented by these dimensions.
        /// </summary>
        /// <returns>The area as a long value to handle large dimensions.</returns>
        /// <example>
        /// <code>
        /// var dims = new Dimensions(1920, 1080);
        /// var area = dims.CalculateArea(); // Returns 2073600
        /// </code>
        /// </example>
        public readonly long CalculateArea()
        {
            return (long)Width * Height;
        }

        /// <summary>
        /// Determines whether the current dimensions are valid for display purposes.
        /// </summary>
        /// <returns>True if both width and height are positive and within maximum limits.</returns>
        /// <remarks>
        /// This method checks that the dimensions are within reasonable bounds
        /// for typical display scenarios.
        /// </remarks>
        public readonly bool IsValidForDisplay()
        {
            return Width > 0 && Height > 0 &&
                   Width <= MaxWidth && Height <= MaxHeight;
        }

        /// <summary>
        /// Scales the dimensions by the specified factor.
        /// </summary>
        /// <param name="factor">The scaling factor to apply.</param>
        /// <returns>A new <see cref="Dimensions"/> instance with scaled values.</returns>
        /// <exception cref="ArgumentException">Thrown when factor is negative or zero.</exception>
        public readonly Dimensions Scale(double factor)
        {
            if (factor <= 0)
            {
                throw new ArgumentException("Scale factor must be positive", nameof(factor));
            }

            return new Dimensions(
                (int)Math.Round(Width * factor),
                (int)Math.Round(Height * factor)
            );
        }

        /// <summary>
        /// Returns a string representation of the dimensions.
        /// </summary>
        /// <returns>A string in the format "Width x Height".</returns>
        public override readonly string ToString()
        {
            return $"{Width} x {Height}";
        }

        /// <summary>
        /// Determines whether two dimensions instances are equal.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns>True if the specified object is equal to the current instance.</returns>
        public override readonly bool Equals(object? obj)
        {
            return obj is Dimensions other && Width == other.Width && Height == other.Height;
        }

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <returns>A hash code for the current instance.</returns>
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Width, Height);
        }

        /// <summary>
        /// Determines whether two dimensions instances are equal.
        /// </summary>
        /// <param name="left">The first dimensions to compare.</param>
        /// <param name="right">The second dimensions to compare.</param>
        /// <returns>True if the dimensions are equal; otherwise, false.</returns>
        public static bool operator ==(Dimensions left, Dimensions right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two dimensions instances are not equal.
        /// </summary>
        /// <param name="left">The first dimensions to compare.</param>
        /// <param name="right">The second dimensions to compare.</param>
        /// <returns>True if the dimensions are not equal; otherwise, false.</returns>
        public static bool operator !=(Dimensions left, Dimensions right)
        {
            return !left.Equals(right);
        }
    }
}
