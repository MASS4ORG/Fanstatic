using Fanstatic.Commands.API.APIModels;
using Fanstatic.Helpers;
using Xunit;

namespace Fanstatic.Test.Commands;

public class PartialClassMergerTests
{
    [Fact]
    public void GroupPartialClasses_ShouldMergeSinglePartialClass()
    {
        // Arrange
        var classes = new List<ClassInfo>
        {
            new(
                Name: "TestClass",
                FullName: "TestNamespace.TestClass",
                Namespace: "TestNamespace",
                SourceFile: "TestClass.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "Method1", ReturnType: "void", Modifiers: "public")
                },
                Properties: new List<PropertyInfo>
                {
                    new(Name: "Property1", Type: "string", Modifiers: "public")
                }
            )
        };

        // Act
        var result = PartialClassMerger.GroupPartialClasses(classes);

        // Assert
        Assert.Single(result);
        Assert.Equal("TestClass", result[0].Name);
        Assert.Equal("TestClass.cs", result[0].SourceFile);
        Assert.Single(result[0].PublicMethods);
        Assert.Single(result[0].Properties);
    }

    [Fact]
    public void GroupPartialClasses_ShouldMergeMultiplePartialClasses()
    {
        // Arrange
        var classes = new List<ClassInfo>
        {
            new(
                Name: "PartialClass",
                FullName: "TestNamespace.PartialClass",
                Namespace: "TestNamespace",
                SourceFile: "PartialClass1.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "Method1", ReturnType: "void", Modifiers: "public")
                },
                Properties: new List<PropertyInfo>
                {
                    new(Name: "Property1", Type: "string", Modifiers: "public")
                },
                Fields: new List<FieldInfo>
                {
                    new(Name: "Field1", Type: "int", Modifiers: "public readonly")
                }
            ),
            new(
                Name: "PartialClass",
                FullName: "TestNamespace.PartialClass",
                Namespace: "TestNamespace",
                SourceFile: "PartialClass2.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "Method2", ReturnType: "string", Modifiers: "public")
                },
                Properties: new List<PropertyInfo>
                {
                    new(Name: "Property2", Type: "int", Modifiers: "public")
                },
                Fields: new List<FieldInfo>
                {
                    new(Name: "Field2", Type: "string", Modifiers: "public readonly")
                }
            ),
            new(
                Name: "PartialClass",
                FullName: "TestNamespace.PartialClass",
                Namespace: "TestNamespace",
                SourceFile: "PartialClass3.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "Method3", ReturnType: "bool", Modifiers: "public static")
                },
                Properties: new List<PropertyInfo>
                {
                    new(Name: "Property3", Type: "double", Modifiers: "public")
                }
            )
        };

        // Act
        var result = PartialClassMerger.GroupPartialClasses(classes);

        // Assert
        Assert.Single(result);
        var mergedClass = result[0];

        Assert.Equal("PartialClass", mergedClass.Name);
        Assert.Equal("TestNamespace.PartialClass", mergedClass.FullName);
        Assert.Equal("TestNamespace", mergedClass.Namespace);
        Assert.Equal("PartialClass1.cs, PartialClass2.cs, PartialClass3.cs", mergedClass.SourceFile);
        Assert.Equal("class", mergedClass.TypeKind);

        // Check that all methods were merged
        Assert.NotNull(mergedClass.PublicMethods);
        Assert.Equal(3, mergedClass.PublicMethods.Count);
        Assert.Contains(mergedClass.PublicMethods, m => m.Name == "Method1");
        Assert.Contains(mergedClass.PublicMethods, m => m.Name == "Method2");
        Assert.Contains(mergedClass.PublicMethods, m => m.Name == "Method3");

        // Check that all properties were merged
        Assert.NotNull(mergedClass.Properties);
        Assert.Equal(3, mergedClass.Properties.Count);
        Assert.Contains(mergedClass.Properties, p => p.Name == "Property1");
        Assert.Contains(mergedClass.Properties, p => p.Name == "Property2");
        Assert.Contains(mergedClass.Properties, p => p.Name == "Property3");

        // Check that all fields were merged
        Assert.NotNull(mergedClass.Fields);
        Assert.Equal(2, mergedClass.Fields.Count);
        Assert.Contains(mergedClass.Fields, f => f.Name == "Field1");
        Assert.Contains(mergedClass.Fields, f => f.Name == "Field2");
    }

    [Fact]
    public void GroupPartialClasses_ShouldHandleDuplicateMembers()
    {
        // Arrange - Create partial classes with duplicate member names
        var classes = new List<ClassInfo>
        {
            new(
                Name: "DuplicateClass",
                FullName: "TestNamespace.DuplicateClass",
                Namespace: "TestNamespace",
                SourceFile: "DuplicateClass1.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "DuplicateMethod", ReturnType: "void", Modifiers: "public")
                },
                Properties: new List<PropertyInfo>
                {
                    new(Name: "DuplicateProperty", Type: "string", Modifiers: "public")
                }
            ),
            new(
                Name: "DuplicateClass",
                FullName: "TestNamespace.DuplicateClass",
                Namespace: "TestNamespace",
                SourceFile: "DuplicateClass2.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "DuplicateMethod", ReturnType: "void", Modifiers: "public") // Same name
                },
                Properties: new List<PropertyInfo>
                {
                    new(Name: "DuplicateProperty", Type: "string", Modifiers: "public") // Same name
                }
            )
        };

        // Act
        var result = PartialClassMerger.GroupPartialClasses(classes);

        // Assert
        Assert.Single(result);
        var mergedClass = result[0];

        // Should contain only one of each duplicate member
        Assert.NotNull(mergedClass.PublicMethods);
        Assert.Single(mergedClass.PublicMethods);
        Assert.Equal("DuplicateMethod", mergedClass.PublicMethods[0].Name);

        Assert.NotNull(mergedClass.Properties);
        Assert.Single(mergedClass.Properties);
        Assert.Equal("DuplicateProperty", mergedClass.Properties[0].Name);
    }

    [Fact]
    public void GroupPartialClasses_ShouldHandleMixedClassTypes()
    {
        // Arrange - Mix of partial and non-partial classes
        var classes = new List<ClassInfo>
        {
            new(
                Name: "RegularClass",
                FullName: "TestNamespace.RegularClass",
                Namespace: "TestNamespace",
                SourceFile: "RegularClass.cs",
                TypeKind: "class"
            ),
            new(
                Name: "PartialClass",
                FullName: "TestNamespace.PartialClass",
                Namespace: "TestNamespace",
                SourceFile: "PartialClass1.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "Method1", ReturnType: "void", Modifiers: "public")
                }
            ),
            new(
                Name: "PartialClass",
                FullName: "TestNamespace.PartialClass",
                Namespace: "TestNamespace",
                SourceFile: "PartialClass2.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "Method2", ReturnType: "string", Modifiers: "public")
                }
            )
        };

        // Act
        var result = PartialClassMerger.GroupPartialClasses(classes);

        // Assert
        Assert.Equal(2, result.Count);

        var regularClass = result.First(c => c.Name == "RegularClass");
        var partialClass = result.First(c => c.Name == "PartialClass");

        Assert.Equal("RegularClass.cs", regularClass.SourceFile);
        Assert.Equal("PartialClass1.cs, PartialClass2.cs", partialClass.SourceFile);

        Assert.NotNull(partialClass.PublicMethods);
        Assert.Equal(2, partialClass.PublicMethods.Count);
    }

    [Fact]
    public void GroupNamespaceClasses_ShouldGroupPartialClassesWithinNamespaces()
    {
        // Arrange
        var namespaceClasses = new Dictionary<string, List<ClassInfo>>
        {
            ["TestNamespace.A"] = new List<ClassInfo>
            {
                new(
                    Name: "PartialClass",
                    FullName: "TestNamespace.A.PartialClass",
                    Namespace: "TestNamespace.A",
                    SourceFile: "PartialClass1.cs",
                    TypeKind: "class",
                    PublicMethods: new List<MethodInfo>
                    {
                        new(Name: "Method1", ReturnType: "void", Modifiers: "public")
                    }
                ),
                new(
                    Name: "PartialClass",
                    FullName: "TestNamespace.A.PartialClass",
                    Namespace: "TestNamespace.A",
                    SourceFile: "PartialClass2.cs",
                    TypeKind: "class",
                    PublicMethods: new List<MethodInfo>
                    {
                        new(Name: "Method2", ReturnType: "string", Modifiers: "public")
                    }
                )
            },
            ["TestNamespace.B"] = new List<ClassInfo>
            {
                new(
                    Name: "RegularClass",
                    FullName: "TestNamespace.B.RegularClass",
                    Namespace: "TestNamespace.B",
                    SourceFile: "RegularClass.cs",
                    TypeKind: "class"
                )
            }
        };

        // Act
        var result = PartialClassMerger.GroupNamespaceClasses(namespaceClasses);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.True(result.ContainsKey("TestNamespace.A"));
        Assert.True(result.ContainsKey("TestNamespace.B"));

        var namespaceA = result["TestNamespace.A"];
        var namespaceB = result["TestNamespace.B"];

        Assert.Single(namespaceA);
        Assert.Single(namespaceB);

        var partialClass = namespaceA[0];
        Assert.Equal("PartialClass1.cs, PartialClass2.cs", partialClass.SourceFile);
        Assert.NotNull(partialClass.PublicMethods);
        Assert.Equal(2, partialClass.PublicMethods.Count);

        var regularClass = namespaceB[0];
        Assert.Equal("RegularClass.cs", regularClass.SourceFile);
    }

    [Fact]
    public void GroupPartialClasses_ShouldHandleEmptyList()
    {
        // Arrange
        var classes = new List<ClassInfo>();

        // Act
        var result = PartialClassMerger.GroupPartialClasses(classes);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GroupPartialClasses_ShouldHandleNullCollections()
    {
        // Arrange
        var classes = new List<ClassInfo>
        {
            new(
                Name: "ClassWithNulls",
                FullName: "TestNamespace.ClassWithNulls",
                Namespace: "TestNamespace",
                SourceFile: "ClassWithNulls1.cs",
                TypeKind: "class",
                PublicMethods: null,
                Properties: null,
                Fields: null
            ),
            new(
                Name: "ClassWithNulls",
                FullName: "TestNamespace.ClassWithNulls",
                Namespace: "TestNamespace",
                SourceFile: "ClassWithNulls2.cs",
                TypeKind: "class",
                PublicMethods: new List<MethodInfo>
                {
                    new(Name: "Method1", ReturnType: "void", Modifiers: "public")
                },
                Properties: null,
                Fields: null
            )
        };

        // Act
        var result = PartialClassMerger.GroupPartialClasses(classes);

        // Assert
        Assert.Single(result);
        var mergedClass = result[0];

        Assert.Equal("ClassWithNulls1.cs, ClassWithNulls2.cs", mergedClass.SourceFile);
        Assert.NotNull(mergedClass.PublicMethods);
        Assert.Single(mergedClass.PublicMethods);
        Assert.Equal("Method1", mergedClass.PublicMethods[0].Name);
    }
}
