namespace Fanstatic.Commands.API.APIModels;

/// <summary>
/// Represents information about a class, including its name, namespace, source file, type kind,
/// public methods, properties, fields, enum values, and accompanying documentation.
/// </summary>
/// <param name="Name">The name of the class.</param>
/// <param name="FullName">The full name of the class, including namespace and containing types.</param>
/// <param name="Namespace">The namespace to which the class belongs.</param>
/// <param name="SourceFile">The source file in which the class is defined.</param>
/// <param name="TypeKind">Specifies the kind of type, such as class, struct, or interface.</param>
/// <param name="PublicMethods">A list of public methods defined within the class.</param>
/// <param name="Properties">A list of properties defined within the class.</param>
/// <param name="Fields">A list of fields defined within the class.</param>
/// <param name="EnumValues">A list of enum values if the class type is an enum.</param>
/// <param name="ClassDocumentation">Documentation information associated with the class.</param>
public record ClassInfo(
    string Name = "",
    string FullName = "",
    string Namespace = "",
    string SourceFile = "",
    string TypeKind = "", // class, enum, record, struct
    List<MethodInfo>? PublicMethods = null,
    List<PropertyInfo>? Properties = null,
    List<FieldInfo>? Fields = null,
    List<EnumValueInfo>? EnumValues = null,
    DocumentationInfo? ClassDocumentation = null
)
{
    /// <summary>
    /// Represents a collection of public method definitions associated with the class.
    /// </summary>
    /// <remarks>
    /// This property contains a list of <see cref="MethodInfo"/> objects, each representing a public method
    /// defined within the class. Each method includes details about its name, parameters, return type,
    /// access modifiers, and any associated metadata or documentation.
    /// </remarks>
    public List<MethodInfo> PublicMethods { get; init; } = PublicMethods ?? new();

    /// <summary>
    /// Represents a collection of property definitions associated with the class.
    /// </summary>
    /// <remarks>
    /// This property contains a list of <see cref="PropertyInfo"/> objects, each representing a property
    /// defined within the class. Each property includes details about its name, type, access modifiers,
    /// accessor methods (getter and/or setter), and any associated metadata or documentation.
    /// </remarks>
    public List<PropertyInfo> Properties { get; init; } = Properties ?? new();

    /// <summary>
    /// Maintains a collection of field definitions associated with the class.
    /// </summary>
    /// <remarks>
    /// This property contains a list of <see cref="FieldInfo"/> objects, each representing a field
    /// declared within the class, including its name, type, modifiers, and any additional metadata.
    /// </remarks>
    public List<FieldInfo> Fields { get; init; } = Fields ?? new();

    /// <summary>
    /// Contains information regarding the values of an enumerated type defined in the class.
    /// </summary>
    /// <remarks>
    /// This property holds a collection of <see cref="EnumValueInfo"/> objects,
    /// which represent individual enumerator values associated with an enum declaration.
    /// </remarks>
    public List<EnumValueInfo> EnumValues { get; init; } = EnumValues ?? new();

    /// <summary>
    /// Represents the documentation information associated with a class, struct, enum, or record in the source code.
    /// </summary>
    /// <remarks>
    /// This property is part of the <see cref="ClassInfo"/> model and is used
    /// to store parsed documentation details for a particular type declaration.
    /// </remarks>
    public DocumentationInfo ClassDocumentation { get; init; } = ClassDocumentation ?? new();
}
