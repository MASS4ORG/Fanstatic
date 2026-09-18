using Fanstatic.Commands.API.APIModels;

namespace Fanstatic.Helpers;

/// <summary>
/// Utility class for merging partial class definitions into single ClassInfo objects.
/// </summary>
public static class PartialClassMerger
{
    /// <summary>
    /// Groups partial classes by their FullName and merges them into single entries.
    /// </summary>
    /// <param name="allClasses">All classes from the project structure</param>
    /// <returns>A list of merged class info objects</returns>
    public static List<ClassInfo> GroupPartialClasses(List<ClassInfo> allClasses)
    {
        var groupedClasses = new List<ClassInfo>();
        var classGroups = allClasses.GroupBy(c => c.FullName);

        foreach (var group in classGroups)
        {
            var classes = group.ToList();
            if (classes.Count == 1)
            {
                // Single class, no merging needed
                groupedClasses.Add(classes[0]);
            }
            else
            {
                // Multiple classes with same FullName - merge them (partial classes)
                var mergedClass = MergePartialClasses(classes);
                groupedClasses.Add(mergedClass);
            }
        }

        return groupedClasses;
    }

    /// <summary>
    /// Groups namespace classes by merging partial classes within each namespace.
    /// </summary>
    /// <param name="namespaceClasses">Dictionary of namespace to classes</param>
    /// <returns>Dictionary with grouped classes per namespace</returns>
    public static Dictionary<string, List<ClassInfo>> GroupNamespaceClasses(
        Dictionary<string, List<ClassInfo>> namespaceClasses)
    {
        ArgumentNullException.ThrowIfNull(namespaceClasses);
        var groupedNamespaces = new Dictionary<string, List<ClassInfo>>();

        foreach (var kvp in namespaceClasses)
        {
            var namespaceName = kvp.Key;
            var classes = kvp.Value;
            var groupedClasses = GroupPartialClasses(classes);
            groupedNamespaces[namespaceName] = groupedClasses;
        }

        return groupedNamespaces;
    }

    /// <summary>
    /// Merges multiple partial class definitions into a single ClassInfo object.
    /// </summary>
    /// <param name="partialClasses">List of partial classes to merge</param>
    /// <returns>A merged ClassInfo object</returns>
    static ClassInfo MergePartialClasses(List<ClassInfo> partialClasses)
    {
        var first = partialClasses[0];
        var sourceFiles = string.Join(", ", partialClasses.Select(c => c.SourceFile).Distinct());

        // Merge all methods, properties, and fields
        var allMethods = new List<MethodInfo>();
        var allProperties = new List<PropertyInfo>();
        var allFields = new List<FieldInfo>();
        var allEnumValues = new List<EnumValueInfo>();

        foreach (var partialClass in partialClasses)
        {
            if (partialClass.PublicMethods.Count > 0)
                allMethods.AddRange(partialClass.PublicMethods);
            if (partialClass.Properties.Count > 0)
                allProperties.AddRange(partialClass.Properties);
            if (partialClass.Fields.Count > 0)
                allFields.AddRange(partialClass.Fields);
            if (partialClass.EnumValues.Count > 0)
                allEnumValues.AddRange(partialClass.EnumValues);
        }

        // Remove duplicates by name (in case the same member is declared in multiple partial files)
        var uniqueMethods = allMethods.GroupBy(m => m.Name).Select(g => g.First()).ToList();
        var uniqueProperties = allProperties.GroupBy(p => p.Name).Select(g => g.First()).ToList();
        var uniqueFields = allFields.GroupBy(f => f.Name).Select(g => g.First()).ToList();
        var uniqueEnumValues = allEnumValues.GroupBy(e => e.Name).Select(g => g.First()).ToList();

        // Use the first class's documentation, or merge if needed
        var mergedDocumentation = first.ClassDocumentation;

        return first with
        {
            SourceFile = sourceFiles,
            PublicMethods = uniqueMethods.Count > 0 ? uniqueMethods : first.PublicMethods,
            Properties = uniqueProperties.Count > 0 ? uniqueProperties : first.Properties,
            Fields = uniqueFields.Count > 0 ? uniqueFields : first.Fields,
            EnumValues = uniqueEnumValues.Count > 0 ? uniqueEnumValues : first.EnumValues,
            ClassDocumentation = mergedDocumentation
        };
    }
}
