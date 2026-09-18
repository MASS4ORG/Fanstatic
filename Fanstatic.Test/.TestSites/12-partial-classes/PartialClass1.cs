using System;

namespace TestProject.Models
{
    /// <summary>
    /// This is a partial class for testing partial class merging functionality.
    /// </summary>
    public partial class PartialTestClass
    {
        /// <summary>
        /// A property defined in the first partial file.
        /// </summary>
        public string FirstProperty { get; set; } = "First";

        /// <summary>
        /// A method defined in the first partial file.
        /// </summary>
        /// <param name="value">The input value</param>
        /// <returns>The processed value</returns>
        public string FirstMethod(string value)
        {
            return $"First: {value}";
        }

        /// <summary>
        /// A field defined in the first partial file.
        /// </summary>
        public readonly int FirstField = 1;
    }
}
