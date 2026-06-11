namespace DaprWorkflowExploration.DaprTestcontainersTests.TestUtils;

internal sealed record ProjectPaths(string ProjectDirectory, string ProjectPath, string ComponentsDirectory)
{
    public static ProjectPaths FromMarkerType(string solutionDirectory, Type markerType, string componentsDirectoryName = "components")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solutionDirectory);
        ArgumentNullException.ThrowIfNull(markerType);

        var assemblyName = markerType.Assembly.GetName().Name
                           ?? throw new InvalidOperationException($"Could not determine assembly name for marker type '{markerType.FullName}'.");

        var projectDirectory = Path.Combine(solutionDirectory, assemblyName);
        if (!Directory.Exists(projectDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Could not find project directory '{projectDirectory}' for assembly '{assemblyName}'.");
        }

        var projectPath = Directory.EnumerateFiles(projectDirectory, "*.csproj", SearchOption.TopDirectoryOnly).SingleOrDefault()
                          ?? throw new InvalidOperationException(
                              $"Expected exactly one .csproj in '{projectDirectory}' for assembly '{assemblyName}'.");

        var componentsDirectory = Path.Combine(projectDirectory, componentsDirectoryName);

        return new ProjectPaths(projectDirectory, projectPath, componentsDirectory);
    }
}
