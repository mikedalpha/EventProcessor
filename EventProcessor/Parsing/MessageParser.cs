using System.Xml.Linq;
using EventProcessor.Abstractions;
using EventProcessor.Model;

namespace EventProcessor.Parsing;

public sealed class MessageParser : IMessageParser
{
    private static readonly XNamespace Ns = "urn:eventprocessor:instrument-message:v1";

    private const string IsinScheme = "ISIN";

    public async Task<InstrumentMessage> ParseAsync(Stream xml, CancellationToken cancellationToken)
    {
        var document = await XDocument.LoadAsync(xml, LoadOptions.None, cancellationToken);
        var root = ResolveMessage(document);

        var header = root.Element(Ns + "Header");
        var messageId = Value(header?.Element(Ns + "MessageId"))
            ?? throw new InvalidDataException("Header/MessageId is missing, so the message cannot be de-duplicated.");

        var product = root.Element(Ns + "Product");

        return new InstrumentMessage(
            messageId,
            Value(product?.Element(Ns + "Name")),
            Value(product?.Element(Ns + "Type")),
            ReadProductIsin(product),
            ReadEvents(root));
    }

    private static XElement ResolveMessage(XDocument document)
    {
        var root = document.Root
            ?? throw new InvalidDataException("The document has no root element.");

        if (root.Name == Ns + "InstrumentMessage")
        {
            return root;
        }

        return root.Element(Ns + "InstrumentMessage")
            ?? throw new InvalidDataException($"Expected an InstrumentMessage element but the root was '{root.Name}'.");
    }

    // Only the product's direct Identifiers, so an underlying's ISIN is never picked up
    private static string? ReadProductIsin(XElement? product) =>
        product?
            .Element(Ns + "Identifiers")?
            .Elements(Ns + "Identifier")
            .Where(id => string.Equals((string?)id.Attribute("scheme"), IsinScheme, StringComparison.OrdinalIgnoreCase))
            .Select(Value)
            .FirstOrDefault(value => value is not null);

    private static IReadOnlyList<InstrumentEvent> ReadEvents(XElement root)
    {
        var events = root.Element(Ns + "Events");

        if (events is null)
        {
            return [];
        }

        return events
            .Elements(Ns + "Event")
            .Select(e => new InstrumentEvent(Value(e.Element(Ns + "Code"))))
            .ToList();
    }

    private static string? Value(XElement? element)
    {
        var text = (string?)element;
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
}
