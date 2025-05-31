using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DataSerializationUtils.Core;

namespace DataSerializationUtils.Json;

public class JsonSerializer : ISerializer
{
    public string Serialize<T>(T obj)
    {
        if (obj == null) return "null";
        return SerializeValue(obj);
    }

    private string SerializeValue(object value)
    {
        if (value == null) return "null";
        
        if (value is string str) return $"\"{EscapeString(str)}\"";
        if (value is bool b) return b.ToString().ToLower();
        if (value is DateTime dt) return $"\"{dt:yyyy-MM-ddTHH:mm:ss}\"";
        if (value.GetType().IsPrimitive) return value.ToString();
        
        if (value is IEnumerable enumerable && value is not string)
        {
            return $"[{string.Join(",", enumerable.Cast<object>().Select(SerializeValue))}]";
        }

        var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var serializedProperties = properties
            .Select(p => $"\"{p.Name}\":{SerializeValue(p.GetValue(value))}")
            .ToList();
            
        return $"{{{string.Join(",", serializedProperties)}}}";
    }

    private string EscapeString(string str)
    {
        return str.Replace("\"", "\\\"")
                 .Replace("\\", "\\\\")
                 .Replace("\n", "\\n")
                 .Replace("\r", "\\r")
                 .Replace("\t", "\\t");
    }

    public T Deserialize<T>(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) throw new ArgumentException("Data cannot be empty");
        
        var token = ParseJson(data.Trim());
        return (T)ConvertToType(token, typeof(T));
    }

    private object ParseJson(string json)
    {
        json = json.Trim();
        if (json.StartsWith("{")) return ParseObject(json);
        if (json.StartsWith("[")) return ParseArray(json);
        if (json.StartsWith("\"")) return ParseString(json);
        if (json == "null") return null;
        if (json == "true") return true;
        if (json == "false") return false;
        if (double.TryParse(json, out double number)) return number;
        
        throw new FormatException($"Unexpected JSON format: {json}");
    }

    private Dictionary<string, object> ParseObject(string json)
    {
        var result = new Dictionary<string, object>();
        if (json.Length <= 2) return result;

        json = json.Substring(1, json.Length - 2);
        var pairs = SplitTokens(json);

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split(new[] { ':' }, 2);
            if (keyValue.Length != 2) throw new FormatException("Invalid object format");

            var key = ParseString(keyValue[0].Trim());
            var value = ParseJson(keyValue[1].Trim());
            result[key] = value;
        }

        return result;
    }

    private List<object> ParseArray(string json)
    {
        var result = new List<object>();
        if (json.Length <= 2) return result;

        json = json.Substring(1, json.Length - 2);
        var elements = SplitTokens(json);

        foreach (var element in elements)
        {
            result.Add(ParseJson(element.Trim()));
        }

        return result;
    }

    private string ParseString(string json)
    {
        json = json.Trim();
        if (!json.StartsWith("\"") || !json.EndsWith("\""))
            throw new FormatException("Invalid string format");

        return UnescapeString(json.Substring(1, json.Length - 2));
    }

    private string UnescapeString(string str)
    {
        return str.Replace("\\\\", "\\")
                 .Replace("\\\"", "\"")
                 .Replace("\\n", "\n")
                 .Replace("\\r", "\r")
                 .Replace("\\t", "\t");
    }

    private List<string> SplitTokens(string json)
    {
        var tokens = new List<string>();
        var currentToken = "";
        var depth = 0;
        var inString = false;

        for (int i = 0; i < json.Length; i++)
        {
            var c = json[i];
            if (c == '\"' && (i == 0 || json[i - 1] != '\\')) inString = !inString;
            else if (!inString)
            {
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']') depth--;
            }

            if (c == ',' && depth == 0 && !inString)
            {
                tokens.Add(currentToken);
                currentToken = "";
            }
            else
            {
                currentToken += c;
            }
        }

        if (!string.IsNullOrEmpty(currentToken))
            tokens.Add(currentToken);

        return tokens;
    }

    private object ConvertToType(object value, Type targetType)
    {
        if (value == null) return null;

        if (targetType == typeof(string)) return value.ToString();
        if (targetType.IsPrimitive) return Convert.ChangeType(value, targetType);
        if (targetType == typeof(DateTime))
        {
            if (value is DateTime dt) return dt;
            if (value is string dateStr) return DateTime.Parse(dateStr);
        }

        if (value is Dictionary<string, object> dict)
        {
            var instance = Activator.CreateInstance(targetType);
            var properties = targetType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                if (dict.TryGetValue(prop.Name, out var propValue))
                {
                    prop.SetValue(instance, ConvertToType(propValue, prop.PropertyType));
                }
            }

            return instance;
        }

        if (value is List<object> list && targetType.IsGenericType)
        {
            var elementType = targetType.GetGenericArguments()[0];
            var convertedList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType));

            foreach (var item in list)
            {
                convertedList.Add(ConvertToType(item, elementType));
            }

            return convertedList;
        }

        throw new InvalidOperationException($"Cannot convert {value.GetType()} to {targetType}");
    }
} 