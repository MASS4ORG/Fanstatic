using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Fanstatic.Parsers;

/// <summary>
/// Required for converting the string into Uri
/// </summary>
public class UriTypeConverter : IYamlTypeConverter
{
    /// <inheritdoc />
    public bool Accepts(Type type)
    {
        return type == typeof(Uri);
    }

    /// <inheritdoc />
    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return new Uri(scalar.Value);
    }

    /// <inheritdoc />
    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(emitter);

        if (value == null)
        {
            return;
        }

        var uri = (Uri)value;
        emitter.Emit(new Scalar(uri.ToString()));
    }
}
