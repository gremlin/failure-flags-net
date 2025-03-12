using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FailureFlags
{
    /// <summary>
    /// Converts JSON to and from effects (ie., dictionary with string keys and object values)
    /// </summary>
    public class EffectConverter : JsonConverter<Dictionary<string, object>>
    {
        public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            var dictionary = new Dictionary<string, object>();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return dictionary;
                }
                if (reader.TokenType != JsonTokenType.PropertyName)
                {
                    throw new JsonException();
                }
                string propertyName = reader.GetString() ?? throw new JsonException("Property name is null");
                reader.Read();
                object value = ReadValue(ref reader) ?? throw new JsonException("Value is null");
                dictionary.Add(propertyName, value);
            }
            return dictionary;
        }

        private object? ReadValue(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString() ?? throw new JsonException("Value is null");
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out int intValue))
                    {
                        return intValue;
                    }
                    return reader.GetDouble();
                case JsonTokenType.StartObject:
                    return Read(ref reader, typeof(Dictionary<string, object>), GremlinFailureFlags.JSON_OPTIONS);
                case JsonTokenType.StartArray:
                    var list = new List<object>();
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        var value = ReadValue(ref reader);
                        if (value != null)
                        {
                            list.Add(value);
                        }
                    }
                    return list;
                case JsonTokenType.True:
                    return true;
                case JsonTokenType.False:
                    return false;
                default:
                    throw new JsonException();
            }
        }
        public override void Write(Utf8JsonWriter writer, Dictionary<string, object> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            foreach (var kvp in value)
            {
                writer.WritePropertyName(kvp.Key);
                WriteValue(writer, kvp.Value, options);
            }
            writer.WriteEndObject();
        }

        private void WriteValue(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case string s:
                    writer.WriteStringValue(s);
                    break;
                case int i:
                    writer.WriteNumberValue(i);
                    break;
                case double d:
                    writer.WriteNumberValue(d);
                    break;
                case bool b:
                    writer.WriteBooleanValue(b);
                    break;
                case null:
                    writer.WriteNullValue();
                    break;
                case Dictionary<string, object> dict:
                    Write(writer, dict, options);
                    break;
                case List<object> list:
                    writer.WriteStartArray();
                    foreach (var item in list)
                    {
                        WriteValue(writer, item, options);
                    }
                    writer.WriteEndArray();
                    break;
                default:
                    JsonSerializer.Serialize(writer, value, options);
                    break;
            }
        }
    }
}