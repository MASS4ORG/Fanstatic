using System;

namespace TestProject.Models
{
    // This is a simple class with only regular comments
    // Used to test the documentation parser's handling of non-XML comments
    public class SimpleClass
    {
        // A simple field
        public string Name;
        
        // Another field with a comment
        public int Value = 42;
        
        // A property with getter and setter
        public bool IsActive { get; set; }
        
        // Constructor that takes a name
        public SimpleClass(string name)
        {
            Name = name;
        }
        
        // A method that does something simple
        public void DoSomething()
        {
            // This method doesn't do much
            IsActive = true;
        }
        
        // Method with parameters and return value
        public string GetDescription(string prefix)
        {
            // Returns a formatted description
            return $"{prefix}: {Name} (Value: {Value})";
        }
    }
    
    // Simple enum without XML documentation
    public enum SimpleStatus
    {
        // First status
        None,
        // Second status  
        Active,
        // Third status
        Inactive
    }
}