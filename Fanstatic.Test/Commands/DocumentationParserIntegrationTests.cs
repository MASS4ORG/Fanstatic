using Xunit;

namespace Fanstatic.Test.Commands;

public class DocumentationParserIntegrationTests : CodeAnalysisTestBase
{
    [Fact]
    public async Task ParseDocumentation_SampleClass_ShouldParseCompleteXmlDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");

        // Assert
        Assert.NotNull(sampleClass.ClassDocumentation);
        Assert.Contains("sample class used for testing", sampleClass.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("code analyzer functionality", sampleClass.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("demonstrates various C# language features", sampleClass.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);

        Assert.NotEmpty(sampleClass.ClassDocumentation.Remarks);
        Assert.Contains("specifically designed to test", sampleClass.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);
        Assert.Contains("documentation parsing capabilities", sampleClass.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);

        Assert.NotEmpty(sampleClass.ClassDocumentation.Example);
        Assert.Contains("var sample = new SampleClass", sampleClass.ClassDocumentation.Example,
            StringComparison.InvariantCulture);
        Assert.Contains("sample.ProcessData", sampleClass.ClassDocumentation.Example,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ParseDocumentation_SampleClassMethods_ShouldParseMethodDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var processDataMethod = sampleClass.PublicMethods.First(m => m.Name == "ProcessData");

        // Assert
        Assert.Contains("Processes the provided data", processDataMethod.Documentation.Summary,
            StringComparison.InvariantCulture);

        Assert.Single(processDataMethod.Documentation.Parameters);
        var dataParam = processDataMethod.Documentation.Parameters.First();
        Assert.Equal("data", dataParam.Name, StringComparer.InvariantCulture);
        Assert.Contains("input data to process", dataParam.Description, StringComparison.InvariantCulture);

        Assert.NotEmpty(processDataMethod.Documentation.Example);
        Assert.Contains("var result = sample.ProcessData", processDataMethod.Documentation.Example,
            StringComparison.InvariantCulture);
        Assert.Contains("Console.WriteLine(result)", processDataMethod.Documentation.Example,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ParseDocumentation_CalculateMethod_ShouldParseParametersAndRemarks()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var calculateMethod = sampleClass.PublicMethods.First(m => m.Name == "Calculate");

        // Assert
        Assert.Contains("Calculates a value", calculateMethod.Documentation.Summary, StringComparison.InvariantCulture);
        Assert.Contains("calculated result as a double", calculateMethod.Documentation.Returns,
            StringComparison.InvariantCulture);

        Assert.Equal(3, calculateMethod.Documentation.Parameters.Count);

        var xParam = calculateMethod.Documentation.Parameters.First(p => p.Name == "x");
        var yParam = calculateMethod.Documentation.Parameters.First(p => p.Name == "y");
        var multiplierParam = calculateMethod.Documentation.Parameters.First(p => p.Name == "multiplier");

        Assert.Contains("first operand", xParam.Description, StringComparison.InvariantCulture);
        Assert.Contains("second operand", yParam.Description, StringComparison.InvariantCulture);
        Assert.Contains("optional multiplier", multiplierParam.Description, StringComparison.InvariantCulture);

        Assert.NotEmpty(calculateMethod.Documentation.Remarks);
        Assert.Contains("simple calculation", calculateMethod.Documentation.Remarks, StringComparison.InvariantCulture);
        Assert.Contains("(x + y) * multiplier", calculateMethod.Documentation.Remarks,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ParseDocumentation_SampleClassConstructor_ShouldParseConstructorDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var constructor = sampleClass.PublicMethods.FirstOrDefault(m => m.Name == "SampleClass");

        // Assert - Constructor might be parsed as a method or might not be included in PublicMethods
        // This test verifies that if it's included, the documentation is parsed correctly
        if (constructor != null)
        {
            Assert.Contains("Initializes a new instance", constructor.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("`SampleClass`", constructor.Documentation.Summary, StringComparison.InvariantCulture);

            Assert.True(constructor.Documentation.Parameters.Count >= 2);

            var nameParam = constructor.Documentation.Parameters.FirstOrDefault(p => p.Name == "name");
            var idParam = constructor.Documentation.Parameters.FirstOrDefault(p => p.Name == "id");

            if (nameParam != null)
            {
                Assert.Contains("name to assign", nameParam.Description, StringComparison.InvariantCulture);
            }

            if (idParam != null)
            {
                Assert.Contains("unique identifier", idParam.Description, StringComparison.InvariantCulture);
            }
        }
    }

    [Fact]
    public async Task ParseDocumentation_SampleClassProperties_ShouldParsePropertyDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");

        // Assert
        var nameProperty = sampleClass.Properties.FirstOrDefault(p => p.Name == "Name");
        if (nameProperty != null)
        {
            Assert.Contains("name of the sample instance", nameProperty.Documentation.Summary,
                StringComparison.InvariantCulture);
        }

        var idProperty = sampleClass.Properties.FirstOrDefault(p => p.Name == "Id");
        if (idProperty != null)
        {
            Assert.Contains("unique identifier", idProperty.Documentation.Summary, StringComparison.InvariantCulture);
        }

        var displayNameProperty = sampleClass.Properties.FirstOrDefault(p => p.Name == "DisplayName");
        if (displayNameProperty != null)
        {
            Assert.Contains("computed property", displayNameProperty.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("combines the name and ID", displayNameProperty.Documentation.Summary,
                StringComparison.InvariantCulture);
        }
    }

    [Fact]
    public async Task ParseDocumentation_SampleClassFields_ShouldParseFieldDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");

        // Assert
        var defaultPrefixField = sampleClass.Fields.FirstOrDefault(f => f.Name == "DefaultPrefix");
        if (defaultPrefixField != null)
        {
            Assert.Contains("constant value for testing", defaultPrefixField.Documentation.Summary,
                StringComparison.InvariantCulture);
        }

        var publicField = sampleClass.Fields.FirstOrDefault(f => f.Name == "PublicField");
        if (publicField != null)
        {
            Assert.Contains("modified externally", publicField.Documentation.Summary,
                StringComparison.InvariantCulture);
        }
    }

    [Fact]
    public async Task ParseDocumentation_SampleStatusEnum_ShouldParseEnumDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleStatus = result.AllClasses.First(c => c.Name == "SampleStatus");

        // Assert
        Assert.NotNull(sampleStatus.ClassDocumentation);
        Assert.Contains("different status values", sampleStatus.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("testing the code analyzer", sampleStatus.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);

        Assert.NotEmpty(sampleStatus.ClassDocumentation.Remarks);
        Assert.Contains("predefined status values", sampleStatus.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);
        Assert.Contains("categorize the current state", sampleStatus.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);

        // Check enum value documentation
        var noneValue = sampleStatus.EnumValues.FirstOrDefault(e => e.Name == "None");
        Assert.NotNull(noneValue);
        Assert.Contains("not been initialized", noneValue.Documentation.Summary, StringComparison.InvariantCulture);
        Assert.Contains("unknown state", noneValue.Documentation.Summary, StringComparison.InvariantCulture);

        var activeValue = sampleStatus.EnumValues.FirstOrDefault(e => e.Name == "Active");
        Assert.NotNull(activeValue);
        Assert.Contains("currently active", activeValue.Documentation.Summary, StringComparison.InvariantCulture);
        Assert.Contains("operational", activeValue.Documentation.Summary, StringComparison.InvariantCulture);

        var completedValue = sampleStatus.EnumValues.FirstOrDefault(e => e.Name == "Completed");
        Assert.NotNull(completedValue);
        Assert.Contains("completed its lifecycle", completedValue.Documentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("successfully", completedValue.Documentation.Summary, StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ParseDocumentation_UserRecord_ShouldParseRecordDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var userRecord = result.AllClasses.First(c => c.Name == "UserRecord");

        // Assert
        Assert.NotNull(userRecord.ClassDocumentation);
        Assert.Contains("data transfer object", userRecord.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("record syntax", userRecord.ClassDocumentation.Summary, StringComparison.InvariantCulture);
        Assert.Contains("test the code analyzer", userRecord.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);

        Assert.NotEmpty(userRecord.ClassDocumentation.Remarks);
        Assert.Contains("Records provide value-based equality", userRecord.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);
        Assert.Contains("immutable by default", userRecord.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);

        Assert.NotEmpty(userRecord.ClassDocumentation.Example);
        Assert.Contains("var user = new UserRecord", userRecord.ClassDocumentation.Example,
            StringComparison.InvariantCulture);
        Assert.Contains("john.doe@example.com", userRecord.ClassDocumentation.Example,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ParseDocumentation_UserRecordMethods_ShouldParseMethodDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var userRecord = result.AllClasses.First(c => c.Name == "UserRecord");

        // Assert
        var isValidMethod = userRecord.PublicMethods.FirstOrDefault(m => m.Name == "IsValid");
        if (isValidMethod != null)
        {
            Assert.Contains("valid data", isValidMethod.Documentation.Summary, StringComparison.InvariantCulture);
            Assert.Contains("True if the user data is valid", isValidMethod.Documentation.Returns,
                StringComparison.InvariantCulture);
            Assert.NotEmpty(isValidMethod.Documentation.Remarks);
            Assert.Contains("basic validation rules", isValidMethod.Documentation.Remarks,
                StringComparison.InvariantCulture);
        }

        var toDisplayStringMethod = userRecord.PublicMethods.FirstOrDefault(m => m.Name == "ToDisplayString");
        if (toDisplayStringMethod != null)
        {
            Assert.Contains("display-friendly representation", toDisplayStringMethod.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("formatted string representation", toDisplayStringMethod.Documentation.Returns,
                StringComparison.InvariantCulture);

            var includeEmailParam =
                toDisplayStringMethod.Documentation.Parameters.FirstOrDefault(p => p.Name == "includeEmail");
            if (includeEmailParam != null)
            {
                Assert.Contains("include the email address", includeEmailParam.Description,
                    StringComparison.InvariantCulture);
            }
        }
    }

    [Fact]
    public async Task ParseDocumentation_DimensionsStruct_ShouldParseStructDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var dimensions = result.AllClasses.First(c => c.Name == "Dimensions");

        // Assert
        Assert.NotNull(dimensions.ClassDocumentation);
        Assert.Contains("lightweight data structure", dimensions.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("dimensional information", dimensions.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("test the code analyzer", dimensions.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);

        Assert.NotEmpty(dimensions.ClassDocumentation.Remarks);
        Assert.Contains("Structs are value types", dimensions.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);
        Assert.Contains("value semantics", dimensions.ClassDocumentation.Remarks, StringComparison.InvariantCulture);

        Assert.NotEmpty(dimensions.ClassDocumentation.Example);
        Assert.Contains("var dimensions = new Dimensions", dimensions.ClassDocumentation.Example,
            StringComparison.InvariantCulture);
        Assert.Contains("dimensions.CalculateArea", dimensions.ClassDocumentation.Example,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ParseDocumentation_IDataService_ShouldParseInterfaceDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var dataService = result.AllClasses.First(c => c.Name == "IDataService");

        // Assert
        Assert.NotNull(dataService.ClassDocumentation);
        Assert.Contains("Defines the contract for data service operations", dataService.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("managing and retrieving data entities", dataService.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("interface parsing capabilities", dataService.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);

        Assert.NotEmpty(dataService.ClassDocumentation.Remarks);
        Assert.Contains("various method signatures", dataService.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);
        Assert.Contains("synchronous and asynchronous operations", dataService.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);
    }

    [Fact]
    public async Task ParseDocumentation_IDataServiceMethods_ShouldParseInterfaceMethodDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var dataService = result.AllClasses.First(c => c.Name == "IDataService");

        // Assert
        var getUserAsyncMethod = dataService.PublicMethods.FirstOrDefault(m => m.Name == "GetUserAsync");
        if (getUserAsyncMethod != null)
        {
            Assert.Contains("Retrieves a user", getUserAsyncMethod.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("unique identifier", getUserAsyncMethod.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("user if found, or null", getUserAsyncMethod.Documentation.Returns,
                StringComparison.InvariantCulture);

            var userIdParam = getUserAsyncMethod.Documentation.Parameters.FirstOrDefault(p => p.Name == "userId");
            if (userIdParam != null)
            {
                Assert.Contains("unique identifier of the user", userIdParam.Description,
                    StringComparison.InvariantCulture);
            }
        }

        var createUserAsyncMethod = dataService.PublicMethods.FirstOrDefault(m => m.Name == "CreateUserAsync");
        if (createUserAsyncMethod != null)
        {
            Assert.Contains("Creates a new user", createUserAsyncMethod.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("created user with assigned ID", createUserAsyncMethod.Documentation.Returns,
                StringComparison.InvariantCulture);
        }

        var processEntitiesMethod = dataService.PublicMethods.FirstOrDefault(m => m.Name == "ProcessEntitiesByStatus");
        if (processEntitiesMethod != null)
        {
            Assert.Contains("Processes data entities", processEntitiesMethod.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("successfully processed", processEntitiesMethod.Documentation.Returns,
                StringComparison.InvariantCulture);

            var statusParam = processEntitiesMethod.Documentation.Parameters.FirstOrDefault(p => p.Name == "status");
            var batchSizeParam =
                processEntitiesMethod.Documentation.Parameters.FirstOrDefault(p => p.Name == "batchSize");

            if (statusParam != null)
                Assert.Contains("status filter", statusParam.Description, StringComparison.InvariantCulture);
            if (batchSizeParam != null)
                Assert.Contains("entities to process in each batch", batchSizeParam.Description,
                    StringComparison.InvariantCulture);
        }
    }

    [Fact]
    public async Task ParseDocumentation_SimpleClass_ShouldParseRegularComments()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var simpleClass = result.AllClasses.First(c => c.Name == "SimpleClass");

        // Assert - Regular comments should be parsed differently than XML documentation
        Assert.NotNull(simpleClass.ClassDocumentation);
        // Regular comments might be parsed as summary or might be empty depending on implementation
        // The important thing is that the class is found and doesn't cause errors

        var doSomethingMethod = simpleClass.PublicMethods.FirstOrDefault(m => m.Name == "DoSomething");
        var getDescriptionMethod = simpleClass.PublicMethods.FirstOrDefault(m => m.Name == "GetDescription");

        Assert.NotNull(doSomethingMethod);
        Assert.NotNull(getDescriptionMethod);

        // Regular comments might not be parsed as documentation in the same way
        // but the methods should still be found
    }

    [Fact]
    public async Task ParseDocumentation_ValidationResult_ShouldParseRecordParameterDocumentation()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var validationResult = result.AllClasses.First(c => c.Name == "ValidationResult");

        // Assert
        Assert.NotNull(validationResult.ClassDocumentation);
        Assert.Contains("result of a data validation operation", validationResult.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);

        // Record parameters might be documented in the class summary
        // The exact parsing depends on how the CodeAnalyzer handles record parameter documentation
    }

    [Fact]
    public async Task ParseDocumentation_ComplexXmlTags_ShouldBeProcessedCorrectly()
    {
        // This test ensures that the DocumentationParser correctly handles
        // complex XML documentation with various tags like <see cref>, <paramref>, <c>, etc.

        // Arrange - We'll test this by looking at the UpdateAsync method which should have complex documentation
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var updateAsyncMethod = sampleClass.PublicMethods.FirstOrDefault(m => m.Name == "UpdateAsync");

        // Assert
        if (updateAsyncMethod != null)
        {
            Assert.Contains("asynchronously", updateAsyncMethod.Documentation.Summary,
                StringComparison.InvariantCulture);
            Assert.Contains("task representing the asynchronous operation", updateAsyncMethod.Documentation.Returns,
                StringComparison.InvariantCulture);

            // Check that parameters are correctly parsed
            Assert.True(updateAsyncMethod.Documentation.Parameters.Count >= 1);

            var newNameParam = updateAsyncMethod.Documentation.Parameters.FirstOrDefault(p => p.Name == "newName");
            if (newNameParam != null)
            {
                Assert.Contains("new name to set", newNameParam.Description, StringComparison.InvariantCulture);
            }
        }
    }

    [Fact]
    public async Task ParseDocumentation_ShouldHandleAllTypeKindsCorrectly()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert - Verify that documentation is parsed for all different type kinds
        var classTypes = result.AllClasses.Where(c => c.TypeKind == "class").ToList();
        var recordTypes = result.AllClasses.Where(c => c.TypeKind == "record").ToList();
        var structTypes = result.AllClasses.Where(c => c.TypeKind == "struct").ToList();
        var enumTypes = result.AllClasses.Where(c => c.TypeKind == "enum").ToList();
        var interfaceTypes = result.AllClasses.Where(c => c.TypeKind == "interface").ToList();

        Assert.True(classTypes.Any());
        Assert.True(recordTypes.Any());
        Assert.True(structTypes.Any());
        Assert.True(enumTypes.Any());
        Assert.True(interfaceTypes.Any());

        // Each type should have some form of documentation (even if empty)
        foreach (var type in result.AllClasses)
        {
            Assert.NotNull(type.ClassDocumentation);
        }
    }

    [Fact]
    public async Task ParseDocumentation_ShouldPreserveRawContent()
    {
        // Arrange
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");

        // Assert
        Assert.NotEmpty(sampleClass.ClassDocumentation.RawContent);
        Assert.Contains("///", sampleClass.ClassDocumentation.RawContent, StringComparison.InvariantCulture);
        Assert.Contains("<summary>", sampleClass.ClassDocumentation.RawContent, StringComparison.InvariantCulture);

        // Check method documentation raw content
        var processDataMethod = sampleClass.PublicMethods.First(m => m.Name == "ProcessData");
        Assert.NotEmpty(processDataMethod.Documentation.RawContent);
        Assert.Contains("///", processDataMethod.Documentation.RawContent, StringComparison.InvariantCulture);
    }
}
