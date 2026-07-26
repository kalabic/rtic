using System.Reflection;
using LibRTIC.Conversation;
using Xunit;

namespace LibRTIC.Tests;

public sealed class PublicApiBoundaryTests
{
    [Fact]
    public void PublicLibRticApiDoesNotExposeOpenAiTypes()
    {
        Assembly assembly = typeof(RTICSessionEvent).Assembly;
        List<string> leaks = [];

        foreach (Type type in assembly.GetExportedTypes())
        {
            InspectType(type, type.FullName ?? type.Name, leaks);
        }

        Assert.True(
            leaks.Count == 0,
            "Public OpenAI SDK type leaks were found:" + Environment.NewLine
            + string.Join(Environment.NewLine, leaks.Order()));
    }

    [Fact]
    public void NeutralEventModelSourcesDoNotImportOpenAiNamespaces()
    {
        string root = FindRepositoryRoot();
        string[] files =
        [
            Path.Combine(root, "src", "LibRTIC", "Conversation", "RTICPayloadModels.cs"),
            Path.Combine(root, "src", "LibRTIC", "Conversation", "RTICSessionEvents.cs"),
        ];

        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            Assert.DoesNotContain("using OpenAI", source, StringComparison.Ordinal);
            Assert.DoesNotContain("OpenAI.", source, StringComparison.Ordinal);
        }
    }

    private static void InspectType(Type type, string path, List<string> leaks)
    {
        InspectSignatureType(type.BaseType, $"{path} base type", leaks);
        foreach (Type contract in type.GetInterfaces())
        {
            InspectSignatureType(contract, $"{path} interface", leaks);
        }

        if (type.IsGenericTypeDefinition)
        {
            foreach (Type argument in type.GetGenericArguments())
            {
                foreach (Type constraint in argument.GetGenericParameterConstraints())
                {
                    InspectSignatureType(
                        constraint,
                        $"{path} generic constraint '{argument.Name}'",
                        leaks);
                }
            }
        }

        const BindingFlags PublicDeclared =
            BindingFlags.Public
            | BindingFlags.Instance
            | BindingFlags.Static
            | BindingFlags.DeclaredOnly;

        foreach (ConstructorInfo constructor in type.GetConstructors(PublicDeclared))
        {
            InspectParameters(constructor.GetParameters(), $"{path} constructor", leaks);
        }

        foreach (MethodInfo method in type.GetMethods(PublicDeclared))
        {
            string memberPath = $"{path}.{method.Name}";
            InspectSignatureType(method.ReturnType, $"{memberPath} return", leaks);
            InspectParameters(method.GetParameters(), memberPath, leaks);
            foreach (Type argument in method.GetGenericArguments())
            {
                foreach (Type constraint in argument.GetGenericParameterConstraints())
                {
                    InspectSignatureType(
                        constraint,
                        $"{memberPath} generic constraint '{argument.Name}'",
                        leaks);
                }
            }
        }

        foreach (PropertyInfo property in type.GetProperties(PublicDeclared))
        {
            InspectSignatureType(
                property.PropertyType,
                $"{path}.{property.Name} property",
                leaks);
            InspectParameters(
                property.GetIndexParameters(),
                $"{path}.{property.Name} indexer",
                leaks);
        }

        foreach (FieldInfo field in type.GetFields(PublicDeclared))
        {
            InspectSignatureType(
                field.FieldType,
                $"{path}.{field.Name} field",
                leaks);
        }

        foreach (EventInfo eventInfo in type.GetEvents(PublicDeclared))
        {
            InspectSignatureType(
                eventInfo.EventHandlerType,
                $"{path}.{eventInfo.Name} event",
                leaks);
        }

        if (typeof(Delegate).IsAssignableFrom(type))
        {
            MethodInfo? invoke = type.GetMethod("Invoke");
            if (invoke is not null)
            {
                InspectSignatureType(invoke.ReturnType, $"{path} delegate return", leaks);
                InspectParameters(invoke.GetParameters(), $"{path} delegate", leaks);
            }
        }

        foreach (Type nested in type.GetNestedTypes(BindingFlags.Public))
        {
            InspectType(nested, nested.FullName ?? $"{path}.{nested.Name}", leaks);
        }
    }

    private static void InspectParameters(
        IEnumerable<ParameterInfo> parameters,
        string path,
        List<string> leaks)
    {
        foreach (ParameterInfo parameter in parameters)
        {
            InspectSignatureType(
                parameter.ParameterType,
                $"{path} parameter '{parameter.Name}'",
                leaks);
        }
    }

    private static void InspectSignatureType(
        Type? type,
        string path,
        List<string> leaks)
    {
        if (type is null)
        {
            return;
        }

        Type inspected = type.HasElementType ? type.GetElementType()! : type;
        if (IsOpenAiType(inspected))
        {
            leaks.Add($"{path}: {inspected.FullName}");
        }

        if (inspected.IsGenericType)
        {
            foreach (Type argument in inspected.GetGenericArguments())
            {
                InspectSignatureType(argument, $"{path} generic argument", leaks);
            }
        }
    }

    private static bool IsOpenAiType(Type type)
        => type.Assembly.GetName().Name == "OpenAI"
            || type.Namespace?.StartsWith("OpenAI", StringComparison.Ordinal) == true;

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "WinRTIC.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the WinRTIC repository root.");
    }
}
