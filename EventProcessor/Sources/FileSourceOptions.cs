namespace EventProcessor.Sources;

public sealed class FileSourceOptions
{
    public const string SectionName = "Source:File";

    public string Path { get; set; } = string.Empty;
}
