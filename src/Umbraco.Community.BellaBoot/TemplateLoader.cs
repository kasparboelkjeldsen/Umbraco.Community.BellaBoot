using System.Reflection;

namespace Umbraco.Community.BellaBoot;

internal static class TemplateLoader
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
    private static readonly string Prefix = typeof(TemplateLoader).Namespace!;

    public static string Load(string name)
    {
        var resourceName = $"{Prefix}.Templates.{name}";
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded template not found: {resourceName}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
