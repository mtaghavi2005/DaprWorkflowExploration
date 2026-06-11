using System.Text.RegularExpressions;

namespace DaprWorkflowExploration.DaprTestcontainersTests.Support;

internal sealed class TestComponentsDirectory : IDisposable
{
    private readonly string directory;

    private TestComponentsDirectory(string directory)
    {
        this.directory = directory;
    }

    public string Path => directory;

    public static TestComponentsDirectory CreateFrom(
        string sourceDirectory,
        ComponentMutationContext context,
        ComponentPreparationOptions options)
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"dapr-components-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        foreach (var sourcePath in Directory.EnumerateFiles(sourceDirectory, "*.yaml", SearchOption.TopDirectoryOnly))
        {
            var destinationPath = System.IO.Path.Combine(directory, System.IO.Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destinationPath, overwrite: true);
        }

        foreach (var fileToDrop in options.ComponentFilesToDrop)
        {
            var filePath = System.IO.Path.Combine(directory, fileToDrop);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }

        foreach (var mutation in options.Mutations)
        {
            var filePath = System.IO.Path.Combine(directory, mutation.ComponentFile);
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Component file '{mutation.ComponentFile}' was not found in '{directory}'.", filePath);
            }

            var value = mutation.ValueFactory(context);
            RewriteComponentMetadataValue(filePath, mutation.ComponentName, mutation.MetadataName, value, mutation.RemoveSecretStoreAuth);
        }

        return new TestComponentsDirectory(directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void RewriteComponentMetadataValue(
        string componentPath,
        string componentName,
        string metadataName,
        string value,
        bool removeSecretStoreAuth)
    {
        var content = File.ReadAllText(componentPath);

        EnsureComponentNameMatches(componentPath, content, componentName);

        var metadataPattern =
            $@"(?ms)^(\s*-\s+name:\s+{Regex.Escape(metadataName)}\s*\r?\n)(?<body>(?:\s+.*\r?\n)*)";

        var metadataRegex = new Regex(metadataPattern);
        if (!metadataRegex.IsMatch(content))
        {
            throw new InvalidOperationException(
                $"Metadata entry '{metadataName}' was not found in component '{componentName}' at '{componentPath}'.");
        }

        content = metadataRegex.Replace(
            content,
            match => $"{match.Groups[1].Value}      value: {value}{Environment.NewLine}",
            count: 1);

        if (removeSecretStoreAuth)
        {
            var authRegex = new Regex(@"(?ms)\r?\nauth:\r?\n\s+secretStore:\s+.+?(?=\r?\n\S|\z)");
            content = authRegex.Replace(content, string.Empty, 1);
        }

        File.WriteAllText(componentPath, content);
    }

    private static void EnsureComponentNameMatches(string componentPath, string content, string expectedComponentName)
    {
        var componentNamePattern = @"(?m)^metadata:\s*\r?\n\s+name:\s+(?<name>\S+)\s*$";
        var match = Regex.Match(content, componentNamePattern);

        if (!match.Success)
        {
            throw new InvalidOperationException($"Could not find metadata.name in component file '{componentPath}'.");
        }

        var actualComponentName = match.Groups["name"].Value;
        if (!string.Equals(actualComponentName, expectedComponentName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Component file '{componentPath}' has metadata.name '{actualComponentName}', expected '{expectedComponentName}'.");
        }
    }
}

internal sealed record ComponentPreparationOptions
{
    public bool RequiresRedisHost { get; init; }
    public IReadOnlyCollection<string> ComponentFilesToDrop { get; init; } = [];
    public IReadOnlyCollection<ComponentMetadataValueMutation> Mutations { get; init; } = [];
}

internal sealed record ComponentMetadataValueMutation(
    string ComponentFile,
    string ComponentName,
    string MetadataName,
    Func<ComponentMutationContext, string> ValueFactory,
    bool RemoveSecretStoreAuth = true);

internal sealed record ComponentMutationContext(string? RedisHost);
