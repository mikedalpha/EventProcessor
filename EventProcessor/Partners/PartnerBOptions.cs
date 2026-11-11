namespace EventProcessor.Partners;

public sealed class PartnerBOptions
{
    public const string SectionName = "Partners:B";

    public string EventType { get; set; } = string.Empty;

    public string OutputDirectory { get; set; } = string.Empty;
}
