using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// Reads a JSON object's exact top-level property names without relying on
/// JsonUtility's default values for missing fields.
/// </summary>
public static class BackendJsonObjectValidator
{
    public static bool HasRequiredTopLevelProperties(string json, params string[] requiredProperties)
    {
        HashSet<string> properties;
        if (!TryReadTopLevelProperties(json, out properties))
            return false;

        foreach (string property in requiredProperties)
        {
            if (string.IsNullOrEmpty(property) || !properties.Contains(property))
                return false;
        }

        return true;
    }

    static bool TryReadTopLevelProperties(string json, out HashSet<string> properties)
    {
        properties = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
            return false;

        int index = 0;
        SkipWhitespace(json, ref index);
        if (!ReadObject(json, ref index, properties))
            return false;
        SkipWhitespace(json, ref index);
        return index == json.Length;
    }

    static bool ReadObject(string json, ref int index, HashSet<string> topLevelProperties)
    {
        if (!Consume(json, ref index, '{'))
            return false;

        SkipWhitespace(json, ref index);
        if (Consume(json, ref index, '}'))
            return true;

        while (index < json.Length)
        {
            string propertyName;
            if (!ReadString(json, ref index, out propertyName))
                return false;
            if (topLevelProperties != null)
                topLevelProperties.Add(propertyName);

            SkipWhitespace(json, ref index);
            if (!Consume(json, ref index, ':'))
                return false;
            SkipWhitespace(json, ref index);
            if (!SkipValue(json, ref index))
                return false;
            SkipWhitespace(json, ref index);

            if (Consume(json, ref index, '}'))
                return true;
            if (!Consume(json, ref index, ','))
                return false;
            SkipWhitespace(json, ref index);
        }

        return false;
    }

    static bool SkipValue(string json, ref int index)
    {
        if (index >= json.Length)
            return false;

        switch (json[index])
        {
            case '"':
                string ignored;
                return ReadString(json, ref index, out ignored);
            case '{':
                return ReadObject(json, ref index, null);
            case '[':
                return ReadArray(json, ref index);
            case 't':
                return ConsumeLiteral(json, ref index, "true");
            case 'f':
                return ConsumeLiteral(json, ref index, "false");
            case 'n':
                return ConsumeLiteral(json, ref index, "null");
            default:
                return ReadNumber(json, ref index);
        }
    }

    static bool ReadArray(string json, ref int index)
    {
        if (!Consume(json, ref index, '['))
            return false;
        SkipWhitespace(json, ref index);
        if (Consume(json, ref index, ']'))
            return true;

        while (index < json.Length)
        {
            if (!SkipValue(json, ref index))
                return false;
            SkipWhitespace(json, ref index);
            if (Consume(json, ref index, ']'))
                return true;
            if (!Consume(json, ref index, ','))
                return false;
            SkipWhitespace(json, ref index);
        }

        return false;
    }

    static bool ReadString(string json, ref int index, out string value)
    {
        value = null;
        if (!Consume(json, ref index, '"'))
            return false;

        StringBuilder builder = new StringBuilder();
        while (index < json.Length)
        {
            char current = json[index++];
            if (current == '"')
            {
                value = builder.ToString();
                return true;
            }
            if (current < 0x20)
                return false;
            if (current != '\\')
            {
                builder.Append(current);
                continue;
            }

            if (index >= json.Length)
                return false;
            char escape = json[index++];
            switch (escape)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    if (index + 4 > json.Length)
                        return false;
                    int codePoint;
                    if (!int.TryParse(json.Substring(index, 4), NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture, out codePoint))
                        return false;
                    builder.Append((char)codePoint);
                    index += 4;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    static bool ReadNumber(string json, ref int index)
    {
        int start = index;
        if (index < json.Length && json[index] == '-')
            index++;
        if (index >= json.Length)
            return false;

        if (json[index] == '0')
        {
            index++;
        }
        else
        {
            if (json[index] < '1' || json[index] > '9')
                return false;
            while (index < json.Length && char.IsDigit(json[index]))
                index++;
        }

        if (index < json.Length && json[index] == '.')
        {
            index++;
            int fractionStart = index;
            while (index < json.Length && char.IsDigit(json[index]))
                index++;
            if (index == fractionStart)
                return false;
        }

        if (index < json.Length && (json[index] == 'e' || json[index] == 'E'))
        {
            index++;
            if (index < json.Length && (json[index] == '+' || json[index] == '-'))
                index++;
            int exponentStart = index;
            while (index < json.Length && char.IsDigit(json[index]))
                index++;
            if (index == exponentStart)
                return false;
        }

        return index > start;
    }

    static bool ConsumeLiteral(string json, ref int index, string literal)
    {
        if (index + literal.Length > json.Length ||
            string.CompareOrdinal(json, index, literal, 0, literal.Length) != 0)
            return false;
        index += literal.Length;
        return true;
    }

    static bool Consume(string json, ref int index, char expected)
    {
        if (index >= json.Length || json[index] != expected)
            return false;
        index++;
        return true;
    }

    static void SkipWhitespace(string json, ref int index)
    {
        while (index < json.Length && char.IsWhiteSpace(json[index]))
            index++;
    }
}
