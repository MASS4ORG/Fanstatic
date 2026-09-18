using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Fanstatic.Parsers;

/// <summary>
/// A custom YAML type converter for dictionaries with string keys and object values.
/// </summary>
public class ParamsConverter : IYamlTypeConverter
{
    static readonly ObjectDeserializer DefaultDeserializer = (obj) => obj;

    /// <summary>
    /// Checks if the converter can handle the specified type.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is a dictionary with string keys and object values, false otherwise.</returns>
    public bool Accepts(Type type)
    {
        return type == typeof(Dictionary<string, object>);
    }

    /// <summary>
    /// Reads a YAML stream and deserializes it into a dictionary.
    /// </summary>
    /// <param name="parser">The YAML parser.</param>
    /// <param name="type">The type of the object to deserialize.</param>
    /// <param name="rootDeserializer"></param>
    /// <returns>A dictionary deserialized from the YAML stream.</returns>
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        ArgumentNullException.ThrowIfNull(parser);

        var dictionary = new Dictionary<string, object>();

        if (!parser.TryConsume<MappingStart>(out _))
        {
            // just to try to consume will move the pointer to the next event
        }

        while (!parser.TryConsume<MappingEnd>(out _))
        {
            ProcessYamlNode(parser, dictionary);
        }

        return dictionary;
    }

    void ProcessYamlNode(IParser parser, Dictionary<string, object> dictionary)
    {
        if (!parser.TryConsume<Scalar>(out var key))
        {
            throw new YamlException("Expected a key.");
        }

        if (parser.TryConsume<SequenceStart>(out _))
        {
            dictionary[key.Value] = ReadSequence(parser);
        }
        else if (parser.TryConsume<MappingStart>(out _))
        {
            dictionary[key.Value] = ReadNestedMapping(parser);
        }
        else
        {
            ReadScalarValue(parser, dictionary, key);
        }
    }

    List<object> ReadSequence(IParser parser)
    {
        var list = new List<object>();
        while (!parser.TryConsume<SequenceEnd>(out _))
        {
            if (parser.Current is MappingStart)
            {
                list.Add(ReadYaml(parser, typeof(object), DefaultDeserializer));
            }
            else if (parser.Current is Scalar)
            {
                if (parser.TryConsume<Scalar>(out var scalar))
                {
                    list.Add(scalar.Value);
                }
            }
            else
            {
                throw new YamlException("Expected a value, a nested mapping, or a sequence end.");
            }
        }

        return list;
    }

    Dictionary<string, object> ReadNestedMapping(IParser parser) =>
        (Dictionary<string, object>)ReadYaml(parser, typeof(object), DefaultDeserializer);

    void ReadScalarValue(IParser parser, Dictionary<string, object> dictionary, Scalar key)
    {
        if (parser.TryConsume<Scalar>(out var value))
        {
            dictionary[key.Value] = value.Value;
        }
    }

    /// <summary>
    /// Writes an object to a YAML stream.
    /// </summary>
    /// <param name="emitter">The YAML emitter.</param>
    /// <param name="value">The object to serialize.</param>
    /// <param name="type">The type of the object to serialize.</param>
    /// <param name="serializer"></param>
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        throw new NotImplementedException();
    }
}
