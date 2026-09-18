using System;

namespace TestProject.Models
{
    /// <summary>
    /// This is the second part of the partial class for testing partial class merging functionality.
    /// </summary>
    public partial class PartialTestClass
    {
        /// <summary>
        /// A property defined in the second partial file.
        /// </summary>
        public string SecondProperty { get; set; } = "Second";

        /// <summary>
        /// A method defined in the second partial file.
        /// </summary>
        /// <param name="value">The input value</param>
        /// <returns>The processed value</returns>
        public string SecondMethod(string value)
        {
            return $"Second: {value}";
        }

        /// <summary>
        /// A field defined in the second partial file.
        /// </summary>
        public readonly int SecondField = 2;

        /// <summary>
        /// A method that combines functionality from both partial files.
        /// </summary>
        /// <returns>Combined result</returns>
        public string CombinedMethod()
        {
            return $"{FirstProperty} + {SecondProperty} = {FirstField + SecondField}";
        }
    }
}
