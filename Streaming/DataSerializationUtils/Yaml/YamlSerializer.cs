using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using DataSerializationUtils.Core;

namespace DataSerializationUtils.Yaml;

public class YamlSerializer : ISerializer
{
    private int _indentLevel = 0;
    private const string IndentString = "  ";
    private readonly HashSet<object> _serializedObjects = new();

    public string Serialize<T>(T obj)
    {
        _serializedObjects.Clear();
        if (obj == null) return "null";
        return SerializeValue(obj).TrimEnd();
    }

    private string SerializeValue(object value, string key = null, bool isRoot = true)
    {
        if (value == null) return "null";

        if (!value.GetType().IsPrimitive && value is not string && !_serializedObjects.Add(value))
        {
            return "null";
        }

        var sb = new StringBuilder();
        var prefix = isRoot ? "" : string.Concat(Enumerable.Repeat(IndentString, _indentLevel));

        if (key != null)
        {
            sb.Append($"{prefix}{key}: ");
        }
        else if (!isRoot)
        {
            sb.Append($"{prefix}- ");
        }

        if (value is string str)
        {
            if (str.Contains('\n') || str.Contains(':') || str.Contains('"'))
            {
                sb.AppendLine($"\"{EscapeString(str)}\"");
            }
            else
            {
                sb.AppendLine(str);
            }
        }
        else if (value is bool b)
        {
            sb.AppendLine(b.ToString().ToLower());
        }
        else if (value is DateTime dt)
        {
            sb.AppendLine($"\"{dt:O}\"");
        }
        else if (value.GetType().IsPrimitive)
        {
            sb.AppendLine(value.ToString());
        }
        else if (value is IEnumerable enumerable && value is not string)
        {
            if (key != null || !isRoot)
            {
                sb.AppendLine();
            }
            _indentLevel++;
            foreach (var item in enumerable)
            {
                sb.Append(SerializeValue(item, null, false));
            }
            _indentLevel--;
        }
        else
        {
            var properties = value.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (key != null || !isRoot)
            {
                sb.AppendLine();
            }
            _indentLevel++;
            foreach (var prop in properties)
            {
                var propValue = prop.GetValue(value);
                if (propValue != null)
                {
                    sb.Append(SerializeValue(propValue, prop.Name, false));
                }
            }
            _indentLevel--;
        }

        return sb.ToString();
    }

    private string EscapeString(string str)
    {
        return str.Replace("\\", "\\\\")
                 .Replace("\"", "\\\"")
                 .Replace("\n", "\\n")
                 .Replace("\r", "\\r")
                 .Replace("\t", "\\t");
    }

    public T Deserialize<T>(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) throw new ArgumentException("Data cannot be empty");

        var lines = data.Split('\n')
                       .Select(l => l.TrimEnd())
                       .Where(l => !string.IsNullOrWhiteSpace(l))
                       .ToList();

        var parsedData = ParseYaml(lines);
        return (T)ConvertToType(parsedData, typeof(T));
    }

    private object ParseYaml(List<string> lines, ref int currentLine, int currentIndent = 0)
    {
        if (currentLine >= lines.Count) return null;

        var line = lines[currentLine];
        var lineIndent = GetIndentLevel(line);

        if (lineIndent < currentIndent)
        {
            return null;
        }

        line = line.Trim();
        
        if (line.StartsWith("\"") && line.EndsWith("\""))
        {
            var value = line.Substring(1, line.Length - 2);
            if (DateTime.TryParse(value, out DateTime dt))
            {
                return dt;
            }
        }

        if (line.StartsWith("- "))
        {
            var list = new List<object>();
            while (currentLine < lines.Count && 
                   GetIndentLevel(lines[currentLine]) >= currentIndent && 
                   lines[currentLine].Trim().StartsWith("- "))
            {
                var value = ParseValue(lines[currentLine].Trim().Substring(2));
                if (value is Dictionary<string, object>)
                {
                    currentLine++;
                    value = ParseYaml(lines, ref currentLine, currentIndent + 1);
                }
                list.Add(value);
                currentLine++;
            }
            currentLine--;
            return list;
        }
        else if (line.Contains(":"))
        {
            var dict = new Dictionary<string, object>();
            while (currentLine < lines.Count && GetIndentLevel(lines[currentLine]) >= currentIndent)
            {
                var colonIndex = lines[currentLine].IndexOf(':');
                if (colonIndex == -1) break;

                var key = lines[currentLine].Substring(0, colonIndex).Trim();
                var valueStr = lines[currentLine].Substring(colonIndex + 1).Trim();
                
                object value;
                if (string.IsNullOrEmpty(valueStr))
                {
                    currentLine++;
                    if (currentLine < lines.Count)
                    {
                        value = ParseYaml(lines, ref currentLine, GetIndentLevel(lines[currentLine]));
                    }
                    else
                    {
                        value = null;
                    }
                }
                else
                {
                    value = ParseValue(valueStr);
                }

                dict[key] = value;
                currentLine++;
            }
            currentLine--;
            return dict;
        }

        return ParseValue(line);
    }

    private object ParseYaml(List<string> lines)
    {
        var currentLine = 0;
        return ParseYaml(lines, ref currentLine);
    }

    private int GetIndentLevel(string line)
    {
        var indent = 0;
        while (indent < line.Length && char.IsWhiteSpace(line[indent]))
        {
            indent++;
        }
        return indent;
    }

    private object ParseValue(string value)
    {
        value = value.Trim();
        
        if (string.IsNullOrEmpty(value) || value == "null") return null;
        if (value == "true") return true;
        if (value == "false") return false;
        
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            var unescaped = UnescapeString(value.Substring(1, value.Length - 2));
            if (DateTime.TryParse(unescaped, out DateTime dt))
            {
                return dt;
            }
            return unescaped;
        }

        if (double.TryParse(value, 
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, 
            out double number))
        {
            return number;
        }

        return value;
    }

    private string UnescapeString(string str)
    {
        return str.Replace("\\\\", "\\")
                 .Replace("\\\"", "\"")
                 .Replace("\\n", "\n")
                 .Replace("\\r", "\r")
                 .Replace("\\t", "\t");
    }

    private object ConvertToType(object value, Type targetType)
    {
        if (value == null) return null;

        if (targetType == typeof(string)) return value.ToString();
        if (targetType.IsPrimitive) return Convert.ChangeType(value, targetType);
        if (targetType == typeof(DateTime))
        {
            if (value is DateTime dt) return dt;
            if (value is string dateStr)
            {
                return DateTime.Parse(dateStr, System.Globalization.CultureInfo.InvariantCulture);
            }
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