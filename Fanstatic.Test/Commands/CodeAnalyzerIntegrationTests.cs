using Xunit;

namespace Fanstatic.Test.Commands;

public class CodeAnalyzerIntegrationTests : CodeAnalysisTestBase
{
    [Theory]
    [InlineData("SampleClass")]
    [InlineData("SimpleClass")]
    [InlineData("UserRecord")]
    [InlineData("Point")]
    [InlineData("Dimensions")]
    [InlineData("SampleStatus")]
    [InlineData("Priority")]
    [InlineData("SimpleStatus")]
    [InlineData("ValidationResult")]
    [InlineData("IDataService")]
    public async Task AnalyzeTestProject_ShouldParseAllExpectedTypes(string expectedType)
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(10, result.AllClasses.Count);

        var typeNames = result.AllClasses.Select(c => c.Name).ToArray();

        // Verify the expected type is found
        Assert.Contains(expectedType, typeNames, StringComparer.InvariantCulture);
    }

    [Theory]
    [InlineData("TestProject.Models", 8)]
    [InlineData("TestProject.Services", 2)]
    public async Task AnalyzeTestProject_ShouldCorrectlyParseNamespaces(string namespaceName, int expectedMinCount)
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        Assert.True(result.NamespaceClasses.ContainsKey(namespaceName));
        var namespaceTypes = result.NamespaceClasses[namespaceName];
        Assert.Equal(expectedMinCount, namespaceTypes.Count);
    }

    [Fact]
    public async Task AnalyzeTestProject_SampleClass_ShouldHaveCorrectStructure()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");

        Assert.Equal("class", sampleClass.TypeKind);
        Assert.Equal("TestProject.Models.SampleClass", sampleClass.FullName);
        Assert.Equal("TestProject.Models", sampleClass.Namespace);
        Assert.Equal("SampleClass.cs", sampleClass.SourceFile);

        // Verify documentation is parsed
        Assert.NotEmpty(sampleClass.ClassDocumentation.Summary);
        Assert.Contains("sample class used for testing",
            sampleClass.ClassDocumentation.Summary, StringComparison.InvariantCulture);

        // Verify methods are found
        Assert.True(sampleClass.PublicMethods.Count >= 5);
        Assert.Contains(sampleClass.PublicMethods, m => m.Name == "ProcessData");
        Assert.Contains(sampleClass.PublicMethods, m => m.Name == "Calculate");
        Assert.Contains(sampleClass.PublicMethods, m => m.Name == "GetSampleItems");
        Assert.Contains(sampleClass.PublicMethods, m => m.Name == "UpdateAsync");

        // Verify properties are found
        Assert.True(sampleClass.Properties.Count >= 4);
        Assert.Contains(sampleClass.Properties, p => p.Name == "Name");
        Assert.Contains(sampleClass.Properties, p => p.Name == "Id");
        Assert.Contains(sampleClass.Properties, p => p.Name == "CreatedAt");
        Assert.Contains(sampleClass.Properties, p => p.Name == "DisplayName");

        // Verify fields are found
        Assert.True(sampleClass.Fields.Count >= 2);
        Assert.Contains(sampleClass.Fields, f => f.Name == "DefaultPrefix");
        Assert.Contains(sampleClass.Fields, f => f.Name == "PublicField");
    }

    [Fact]
    public async Task AnalyzeTestProject_ProcessDataMethod_ShouldHaveCorrectSignature()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var processDataMethod = sampleClass.PublicMethods.First(m => m.Name == "ProcessData");

        Assert.Equal("string", processDataMethod.ReturnType);
        Assert.Single(processDataMethod.Parameters);
        Assert.Equal("data", processDataMethod.Parameters[0].Name);
        Assert.Equal("string", processDataMethod.Parameters[0].Type);

        // Verify documentation
        Assert.Contains("Processes the provided data",
            processDataMethod.Documentation.Summary, StringComparison.InvariantCulture);
        Assert.Single(processDataMethod.Documentation.Parameters);
        Assert.Equal("data", processDataMethod.Documentation.Parameters[0].Name);
    }

    [Fact]
    public async Task AnalyzeTestProject_CalculateMethod_ShouldHaveDefaultParameters()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var calculateMethod = sampleClass.PublicMethods.First(m => m.Name == "Calculate");

        Assert.Equal("double", calculateMethod.ReturnType);
        Assert.Equal(3, calculateMethod.Parameters.Count);

        var multiplierParam = calculateMethod.Parameters.First(p => p.Name == "multiplier");
        Assert.Equal("double", multiplierParam.Type);
        Assert.Equal("1.0", multiplierParam.DefaultValue);
    }

    [Fact]
    public async Task AnalyzeTestProject_SampleStatus_ShouldHaveCorrectEnumValues()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var sampleStatus = result.AllClasses.First(c => c.Name == "SampleStatus");

        Assert.Equal("enum", sampleStatus.TypeKind, StringComparer.InvariantCulture);
        Assert.Equal(6, sampleStatus.EnumValues.Count);

        var noneValue = sampleStatus.EnumValues.First(e => e.Name == "None");
        Assert.Equal("0", noneValue.Value);
        Assert.Contains("not been initialized", noneValue.Documentation.Summary, StringComparison.InvariantCulture);

        var completedValue = sampleStatus.EnumValues.First(e => e.Name == "Completed");
        Assert.Equal("100", completedValue.Value);

        var failedValue = sampleStatus.EnumValues.First(e => e.Name == "Failed");
        Assert.Equal("999", failedValue.Value);
    }

    [Fact]
    public async Task AnalyzeTestProject_Priority_ShouldHaveImplicitEnumValues()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var priority = result.AllClasses.First(c => c.Name == "Priority");

        Assert.Equal("enum", priority.TypeKind, StringComparer.InvariantCulture);
        Assert.Equal(4, priority.EnumValues.Count);

        var lowValue = priority.EnumValues.First(e => e.Name == "Low");
        Assert.Null(lowValue.Value); // No explicit value assigned

        var normalValue = priority.EnumValues.First(e => e.Name == "Normal");
        Assert.Null(normalValue.Value);

        var highValue = priority.EnumValues.First(e => e.Name == "High");
        Assert.Null(highValue.Value);

        var criticalValue = priority.EnumValues.First(e => e.Name == "Critical");
        Assert.Null(criticalValue.Value);
    }

    [Fact]
    public async Task AnalyzeTestProject_UserRecord_ShouldHaveCorrectStructure()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var userRecord = result.AllClasses.First(c => c.Name == "UserRecord");

        Assert.Equal("record", userRecord.TypeKind, StringComparer.InvariantCulture);
        Assert.Equal("TestProject.Models.UserRecord", userRecord.FullName);

        // Verify documentation
        Assert.Contains("data transfer object", userRecord.ClassDocumentation.Summary,
            StringComparison.InvariantCulture);
        Assert.Contains("Records provide value-based equality", userRecord.ClassDocumentation.Remarks,
            StringComparison.InvariantCulture);
        Assert.Contains("var user = new UserRecord", userRecord.ClassDocumentation.Example,
            StringComparison.InvariantCulture);

        // Verify methods
        Assert.True(userRecord.PublicMethods.Count >= 2);
        Assert.Contains(userRecord.PublicMethods, m => m.Name == "IsValid");
        Assert.Contains(userRecord.PublicMethods, m => m.Name == "ToDisplayString");

        var toDisplayStringMethod = userRecord.PublicMethods.First(m => m.Name == "ToDisplayString");
        Assert.Single(toDisplayStringMethod.Parameters);
        Assert.Equal("includeEmail", toDisplayStringMethod.Parameters[0].Name);
        Assert.Equal("true", toDisplayStringMethod.Parameters[0].DefaultValue);
    }

    [Fact]
    public async Task AnalyzeTestProject_Point_ShouldBeRecordStruct()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var point = result.AllClasses.First(c => c.Name == "Point");

        Assert.Equal("record", point.TypeKind); // record struct is parsed as record
        Assert.True(point.PublicMethods.Count >= 2);
        Assert.Contains(point.PublicMethods, m => m.Name == "DistanceFromOrigin");
        Assert.Contains(point.PublicMethods, m => m.Name == "DistanceTo");

        var distanceToMethod = point.PublicMethods.First(m => m.Name == "DistanceTo");
        Assert.Single(distanceToMethod.Parameters);
        Assert.Equal("other", distanceToMethod.Parameters[0].Name);
        Assert.Equal("Point", distanceToMethod.Parameters[0].Type);
    }

    [Fact]
    public async Task AnalyzeTestProject_Dimensions_ShouldHaveStructMembers()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var dimensions = result.AllClasses.First(c => c.Name == "Dimensions");

        Assert.Equal("struct", dimensions.TypeKind, StringComparer.InvariantCulture);

        // Verify fields (constants)
        Assert.True(dimensions.Fields.Count >= 2);
        var maxWidthField = dimensions.Fields.FirstOrDefault(f => f.Name == "MaxWidth");
        var maxHeightField = dimensions.Fields.FirstOrDefault(f => f.Name == "MaxHeight");

        Assert.NotNull(maxWidthField);
        Assert.NotNull(maxHeightField);
        Assert.Contains("const", maxWidthField.Modifiers, StringComparison.InvariantCulture);
        Assert.Contains("const", maxHeightField.Modifiers, StringComparison.InvariantCulture);

        // Verify properties
        Assert.True(dimensions.Properties.Count >= 4);
        Assert.Contains(dimensions.Properties, p => p.Name == "Width");
        Assert.Contains(dimensions.Properties, p => p.Name == "Height");
        Assert.Contains(dimensions.Properties, p => p.Name == "AspectRatio");
        Assert.Contains(dimensions.Properties, p => p.Name == "IsSquare");

        // Verify methods
        Assert.True(dimensions.PublicMethods.Count >= 3);
        Assert.Contains(dimensions.PublicMethods, m => m.Name == "CalculateArea");
        Assert.Contains(dimensions.PublicMethods, m => m.Name == "IsValidForDisplay");
        Assert.Contains(dimensions.PublicMethods, m => m.Name == "Scale");

        var scaleMethod = dimensions.PublicMethods.First(m => m.Name == "Scale");
        Assert.Equal("Dimensions", scaleMethod.ReturnType);
        Assert.Single(scaleMethod.Parameters);
        Assert.Equal("factor", scaleMethod.Parameters[0].Name);
        Assert.Equal("double", scaleMethod.Parameters[0].Type);
    }

    [Fact]
    public async Task AnalyzeTestProject_IDataService_ShouldHaveInterfaceMethods()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var dataService = result.AllClasses.First(c => c.Name == "IDataService");

        Assert.Equal("interface", dataService.TypeKind, StringComparer.InvariantCulture);
        Assert.Equal("TestProject.Services", dataService.Namespace);

        Assert.True(dataService.PublicMethods.Count >= 7);

        var getUserMethod = dataService.PublicMethods.First(m => m.Name == "GetUserAsync");
        Assert.Equal("Task<UserRecord?>", getUserMethod.ReturnType);
        Assert.Single(getUserMethod.Parameters);
        Assert.Equal("userId", getUserMethod.Parameters[0].Name);

        var getUsersMethod = dataService.PublicMethods.First(m => m.Name == "GetUsersAsync");
        Assert.Equal(2, getUsersMethod.Parameters.Count);
        Assert.Equal("100", getUsersMethod.Parameters[1].DefaultValue, StringComparer.InvariantCulture);

        var processEntitiesMethod = dataService.PublicMethods.First(m => m.Name == "ProcessEntitiesByStatus");
        Assert.Equal("int", processEntitiesMethod.ReturnType);
        Assert.Equal(2, processEntitiesMethod.Parameters.Count);
        Assert.Equal("50", processEntitiesMethod.Parameters[1].DefaultValue);
    }

    [Fact]
    public async Task AnalyzeTestProject_ValidationResult_ShouldBeRecord()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var validationResult = result.AllClasses.First(c => c.Name == "ValidationResult");

        Assert.Equal("record", validationResult.TypeKind, StringComparer.InvariantCulture);
        Assert.Equal("TestProject.Services", validationResult.Namespace);

        // Record parameters should be parsed as properties in some cases
        // or might not show up as traditional properties since they're primary constructor parameters
        Assert.NotNull(validationResult.ClassDocumentation);
    }

    [Fact]
    public async Task AnalyzeTestProject_SimpleClass_ShouldParseRegularComments()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var simpleClass = result.AllClasses.First(c => c.Name == "SimpleClass");

        Assert.Equal("class", simpleClass.TypeKind, StringComparer.InvariantCulture);

        // Verify that regular comments are parsed
        Assert.Contains(simpleClass.PublicMethods, m => m.Name == "DoSomething");
        Assert.Contains(simpleClass.PublicMethods, m => m.Name == "GetDescription");

        var getDescriptionMethod = simpleClass.PublicMethods.First(m => m.Name == "GetDescription");
        Assert.Single(getDescriptionMethod.Parameters);
        Assert.Equal("prefix", getDescriptionMethod.Parameters[0].Name);
        Assert.Equal("string", getDescriptionMethod.Parameters[0].Type);
    }

    [Fact]
    public async Task AnalyzeTestProject_SimpleStatus_ShouldParseRegularCommentedEnum()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var simpleStatus = result.AllClasses.First(c => c.Name == "SimpleStatus");

        Assert.Equal("enum", simpleStatus.TypeKind, StringComparer.InvariantCulture);
        Assert.Equal(3, simpleStatus.EnumValues.Count);

        Assert.Contains(simpleStatus.EnumValues, e => e.Name == "None");
        Assert.Contains(simpleStatus.EnumValues, e => e.Name == "Active");
        Assert.Contains(simpleStatus.EnumValues, e => e.Name == "Inactive");
    }

    [Fact]
    public async Task AnalyzeTestProject_ShouldGroupTypesByNamespace()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var modelsNamespace = result.NamespaceClasses["TestProject.Models"];
        var servicesNamespace = result.NamespaceClasses["TestProject.Services"];

        // Verify Models namespace contains expected types
        var modelsTypeNames = modelsNamespace.Select(c => c.Name).ToArray();
        Assert.Contains("SampleClass", modelsTypeNames);
        Assert.Contains("SimpleClass", modelsTypeNames);
        Assert.Contains("UserRecord", modelsTypeNames);
        Assert.Contains("Point", modelsTypeNames);
        Assert.Contains("Dimensions", modelsTypeNames);
        Assert.Contains("SampleStatus", modelsTypeNames);
        Assert.Contains("Priority", modelsTypeNames);
        Assert.Contains("SimpleStatus", modelsTypeNames);

        // Verify Services namespace contains expected types
        var servicesTypeNames = servicesNamespace.Select(c => c.Name).ToArray();
        Assert.Contains("IDataService", servicesTypeNames);
        Assert.Contains("ValidationResult", servicesTypeNames);
    }

    [Fact]
    public async Task AnalyzeTestProject_ShouldHandleComplexDocumentation()
    {
        // Act
        var result = await Analyzer.AnalyzeProjectAsync(TestProjectPath);

        // Assert
        var sampleClass = result.AllClasses.First(c => c.Name == "SampleClass");
        var updateAsyncMethod = sampleClass.PublicMethods.FirstOrDefault(m => m.Name == "UpdateAsync");

        if (updateAsyncMethod != null)
        {
            Assert.Contains("async", updateAsyncMethod.Documentation.Summary, StringComparison.InvariantCulture);
            Assert.Equal(2, updateAsyncMethod.Parameters.Count);

            var delayParam = updateAsyncMethod.Parameters.FirstOrDefault(p => p.Name == "delay");
            if (delayParam != null)
            {
                Assert.Equal("0", delayParam.DefaultValue);
            }
        }
    }
}
