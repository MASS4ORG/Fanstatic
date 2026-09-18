using System;

namespace TestProject.Models
{
    /// <summary>
    /// This is the third part of the partial class for testing partial class merging functionality.
    /// </summary>
    public partial class PartialTestClass
    {
        /// <summary>
        /// A property defined in the third partial file.
        /// </summary>
        public string ThirdProperty { get; set; } = "Third";

        /// <summary>
        /// A method defined in the third partial file.
        /// </summary>
        /// <param name="value">The input value</param>
        /// <returns>The processed value</returns>
        public string ThirdMethod(string value)
        {
            return $"Third: {value}";
        }

        /// <summary>
        /// A field defined in the third partial file.
        /// </summary>
        public readonly int ThirdField = 3;

        /// <summary>
        /// A static method that demonstrates static members in partial classes.
        /// </summary>
        /// <returns>A static value</returns>
        public static string StaticMethod()
        {
            return "Static method from third partial";
        }

        /// <summary>
        /// A property that uses all three fields.
        /// </summary>
        public int TotalFields => FirstField + SecondField + ThirdField;
    }
}
