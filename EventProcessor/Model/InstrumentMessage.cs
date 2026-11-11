namespace EventProcessor.Model;

public sealed record InstrumentMessage(
    string MessageId,
    string? ProductName,
    string? ProductType,
    string? Isin,
    IReadOnlyList<InstrumentEvent> Events);
