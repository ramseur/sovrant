using System.Text;

namespace Sovrant.Tools.Projects.Scaffolds;

/// <summary>Name-conversion utilities shared by all scaffold implementations.</summary>
internal static class ScaffoldHelpers
{
    /// <summary>"my-project" | "my_project" | "MyProject" → "MyProject"</summary>
    public static string ToPascalCase(string name)
    {
        var sb = new StringBuilder(name.Length);
        var capitalizeNext = true;
        foreach (var c in name)
        {
            if (c is '-' or '_' or ' ')
            {
                capitalizeNext = true;
            }
            else if (capitalizeNext)
            {
                sb.Append(char.ToUpperInvariant(c));
                capitalizeNext = false;
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.Length == 0 ? "Project" : sb.ToString();
    }

    /// <summary>"MyProject" | "my-project" → "my_project"</summary>
    public static string ToSnakeCase(string name)
    {
        var pascal = ToPascalCase(name);
        var sb = new StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            var c = pascal[i];
            if (i > 0 && char.IsUpper(c))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>"MyProject" | "my_project" → "my-project"</summary>
    public static string ToKebabCase(string name) => ToSnakeCase(name).Replace('_', '-');

    /// <summary>Strips all non-alphanumeric chars; result is safe as a Go/Java single-word package name.</summary>
    public static string ToWordPackage(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var c in ToSnakeCase(name))
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
        }
        return sb.Length == 0 ? "app" : sb.ToString();
    }

    /// <summary>Java/Kotlin reverse-domain package: "com.example.{wordpackage}"</summary>
    public static string ToJavaPackage(string name) => $"com.example.{ToWordPackage(name)}";
}
