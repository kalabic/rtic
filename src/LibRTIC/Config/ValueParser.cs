using System.Text.Json.Nodes;
using System.Text.Json;
using LibRTIC.MiniTaskLib.Model;

namespace LibRTIC.Config;

public class ValueParser
{
    static public int AssertNodeParamIsNullOrArray(Info info, JsonNode node, string paramName)
    {
        var paramNode = node![paramName]!;
        if ((paramNode is null) || (paramNode.GetValueKind() == JsonValueKind.Undefined))
        {
            return 0;
        }

        if ((paramNode is not null) && (paramNode.GetValueKind() == JsonValueKind.Array))
        {
            return 1;
        }

        info.Error($" * Error: Value '{paramName}' not an array.");
        return -1;
    }

    static public int AssertNodeParamIsNullOrObject(Info info, JsonNode node, string paramName)
    {
        var paramNode = node![paramName]!;
        if ((paramNode is null) || (paramNode.GetValueKind() == JsonValueKind.Undefined))
        {
            return 0;
        }

        if ((paramNode is not null) && (paramNode.GetValueKind() == JsonValueKind.Object))
        {
            return 1;
        }

        info.Error($" * Error: Value '{paramName}' not an object.");
        return -1;
    }

    static public int AssertNodeParamIsNullOrString(Info info, JsonNode node, string paramName)
    {
        var paramNode = node![paramName]!;
        if ((paramNode is null) || (paramNode.GetValueKind() == JsonValueKind.Undefined))
        {
            return 0;
        }

        if ((paramNode is not null) && (paramNode.GetValueKind() == JsonValueKind.String))
        {
            return 1;
        }

        info.Error($" * Error: Value '{paramName}' not a string.");
        return -1;
    }

    static public int AssertReadNodeStringParam(Info info, JsonNode node, string paramName, Action<string> reader)
    {
        int assert = AssertNodeParamIsNullOrString(info, node, paramName);
        if (assert == 1)
        {
            reader(node![paramName]!.GetValue<string>());
        }
        return assert;
    }

    static public int AssertReadNodeStringParam(Info info, JsonNode node, string paramName, Func<string, int> reader)
    {
        int assert = AssertNodeParamIsNullOrString(info, node, paramName);
        if (assert == 1)
        {
            return reader(node![paramName]!.GetValue<string>());
        }
        return assert;
    }

    static public int AssertNodeParamIsNullOrBool(Info info, JsonNode node, string paramName)
    {
        var paramNode = node![paramName]!;
        if ((paramNode is null) || (paramNode.GetValueKind() == JsonValueKind.Undefined))
        {
            return 0;
        }

        if ((paramNode is not null) && ((paramNode.GetValueKind() == JsonValueKind.False) || (paramNode.GetValueKind() == JsonValueKind.True)))
        {
            return 1;
        }

        info.Error($" * Error: Value '{paramName}' not a bool.");
        return -1;
    }

    static public int AssertReadNodeBoolParam(Info info, JsonNode node, string paramName, Action<bool> reader)
    {
        Func<bool, int> defaultAction = (value) => { reader(value); return 1; };
        return AssertReadNodeBoolParam(info, node, paramName, defaultAction);
    }

    static public int AssertReadNodeBoolParam(Info info, JsonNode node, string paramName, Func<bool, int> reader)
    {
        int assert = AssertNodeParamIsNullOrBool(info, node, paramName);
        if (assert == 1)
        {
            return reader(node![paramName]!.GetValue<bool>());
        }
        return assert;
    }

    static public int AssertNodeParamIsNullOrNumber(Info info, JsonNode node, string paramName)
    {
        var paramNode = node![paramName]!;
        if ((paramNode is null) || (paramNode.GetValueKind() == JsonValueKind.Undefined))
        {
            return 0;
        }

        if ((paramNode is not null) && (paramNode.GetValueKind() == JsonValueKind.Number))
        {
            return 1;
        }

        info.Error($" * Error: Value '{paramName}' not a number.");
        return -1;
    }

    static public int AssertReadNodeFloatParamInRange(Info info, JsonNode node, string paramName, float minValue, float maxValue, Action<float> reader)
    {
        Func<float, int> defaultAction = (value) => { reader(value); return 1; };
        return AssertReadNodeFloatParamInRange(info, node, paramName, minValue, maxValue, defaultAction);
    }

    static public int AssertReadNodeFloatParamInRange(Info info, JsonNode node, string paramName, float minValue, float maxValue, Func<float, int> reader)
    {
        int assert = AssertNodeParamIsNullOrNumber(info, node, paramName);
        if (assert == 1)
        {
            float value = 0.0f;
            string stringValue = node![paramName]!.ToString();
            if (float.TryParse(stringValue, out value))
            {
                if (value >= minValue && value <= maxValue)
                {
                    return reader(value);
                }
                else
                {
                    info.Error($" * Error: Value '{paramName}' not in range: {stringValue}");
                    return -1;
                }
            }

            info.Error($" * Error: Value '{paramName}' could not be parsed into a float: {stringValue}");
            return -1;
        }
        return assert;
    }

    static public int AssertReadNodeIntParamInRange(Info info, JsonNode node, string paramName, int minValue, int maxValue, Action<int> reader)
    {
        Func<int, int> defaultAction = (value) => { reader(value); return 1; };
        return AssertReadNodeIntParamInRange(info, node, paramName, minValue, maxValue, defaultAction);
    }

    static public int AssertReadNodeIntParamInRange(Info info, JsonNode node, string paramName, int minValue, int maxValue, Func<int, int> reader)
    {
        int assert = AssertNodeParamIsNullOrNumber(info, node, paramName);
        if (assert == 1)
        {
            int value = 0;
            string stringValue = node![paramName]!.ToString();
            if (int.TryParse(stringValue, out value))
            {
                if (value >= minValue && value <= maxValue)
                {
                    return reader(value);
                }
                else
                {
                    info.Error($" * Error: Value '{paramName}' not in range: {stringValue}");
                    return -1;
                }
            }

            info.Error($" * Error: Value '{paramName}' could not be parsed into an int: {stringValue}");
            return -1;
        }
        return assert;
    }
}
