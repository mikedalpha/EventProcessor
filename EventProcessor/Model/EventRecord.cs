namespace EventProcessor.Model;

public sealed record EventRecord(
    string MessageId,
    string? EventType,
    DateTimeOffset ReceivedAtUtc);
