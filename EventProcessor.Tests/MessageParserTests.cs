using System.Text;
using EventProcessor.Parsing;
using Xunit.Abstractions;

namespace EventProcessor.Tests;

public class MessageParserTests(ITestOutputHelper output)
{
    private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "Samples", "sample-message.xml");

    private static Stream StreamOf(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public async Task ParseAsync_WhenGivenTheSuppliedSample_ReadsTheFourRequiredFields()
    {
        // Arrange
        await using var xml = File.OpenRead(SamplePath);

        // Act
        var message = await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        output.WriteLine($"MessageId   = {message.MessageId}");
        output.WriteLine($"ProductName = {message.ProductName}");
        output.WriteLine($"ProductType = {message.ProductType}");
        output.WriteLine($"ISIN        = {message.Isin}");
        output.WriteLine($"Events      = {message.Events.Count}");

        Assert.Equal("3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10", message.MessageId);
        Assert.Equal("8.00% p.a. Barrier Reverse Convertible on Example Corp, Sample Industries", message.ProductName);
        Assert.Equal("ReverseConvertible", message.ProductType);
        Assert.Equal("ISSUED", Assert.Single(message.Events).EventType);
    }

    // The sample carries three ISIN identifiers. Two belong to the underlying shares of the
    // barrier reverse convertible, so this asserts the literal product ISIN rather than
    // just "an ISIN was found", which is what a descendant search would also satisfy.
    [Fact]
    public async Task ParseAsync_WhenTheUnderlyingsCarryTheirOwnIsins_TakesTheProductIsin()
    {
        // Arrange
        await using var xml = File.OpenRead(SamplePath);

        // Act
        var message = await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        Assert.Equal("XX1234567890", message.Isin);
        Assert.NotEqual("XX0000000011", message.Isin);
        Assert.NotEqual("XX0000000022", message.Isin);
    }

    // In the supplied sample the product's ids happen to precede the underlyings, so a
    // descendant search would pass the test above by luck. Here the order is reversed,
    // which a descendant search cannot survive.
    [Fact]
    public async Task ParseAsync_WhenAnUnderlyingIsinComesFirstInTheDocument_StillTakesTheProductIsin()
    {
        // Arrange
        await using var xml = StreamOf("""
            <InstrumentMessage xmlns="urn:eventprocessor:instrument-message:v1">
                <Header>
                    <MessageId>3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10</MessageId>
                </Header>
                <Product>
                    <Underlyings>
                        <Underlying>
                            <Identifiers>
                                <Identifier scheme="ISIN">XX0000000011</Identifier>
                            </Identifiers>
                        </Underlying>
                    </Underlyings>
                    <Identifiers>
                        <Identifier scheme="ISIN">XX1234567890</Identifier>
                    </Identifiers>
                </Product>
            </InstrumentMessage>
            """);

        // Act
        var message = await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        Assert.Equal("XX1234567890", message.Isin);
    }

    [Fact]
    public async Task ParseAsync_WhenTheProductHasSeveralIdentifiers_TakesTheIsinOne()
    {
        // Arrange
        await using var xml = StreamOf("""
            <InstrumentMessage xmlns="urn:eventprocessor:instrument-message:v1">
                <Header>
                    <MessageId>3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10</MessageId>
                </Header>
                <Product>
                    <Identifiers>
                        <Identifier scheme="INTERNAL">PRD-000123</Identifier>
                        <Identifier scheme="ISIN">XX1234567890</Identifier>
                    </Identifiers>
                </Product>
            </InstrumentMessage>
            """);

        // Act
        var message = await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        Assert.Equal("XX1234567890", message.Isin);
    }

    [Fact]
    public async Task ParseAsync_WhenTheMessageCarriesSeveralEvents_ReadsEveryOne()
    {
        // Arrange
        await using var xml = StreamOf("""
            <InstrumentMessage xmlns="urn:eventprocessor:instrument-message:v1">
                <Header>
                    <MessageId>3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10</MessageId>
                </Header>
                <Events>
                    <Event><Code>ISSUED</Code></Event>
                    <Event><Code>REPRICED</Code></Event>
                </Events>
            </InstrumentMessage>
            """);

        // Act
        var message = await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        Assert.Equal(new[] { "ISSUED", "REPRICED" }, message.Events.Select(e => e.EventType).ToArray());
    }

    // Messages may arrive either bare or inside an Envelope, so the parser accepts both.
    // This covers the branch the sample never reaches.
    [Fact]
    public async Task ParseAsync_WhenTheMessageIsWrappedInAnEnvelope_ReadsItAnyway()
    {
        // Arrange
        await using var xml = StreamOf("""
            <Envelope xmlns="urn:eventprocessor:instrument-message:v1">
                <InstrumentMessage>
                    <Header>
                        <MessageId>3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10</MessageId>
                    </Header>
                    <Events>
                        <Event><Code>ISSUED</Code></Event>
                    </Events>
                    <Product>
                        <Name>A wrapped product</Name>
                        <Type>ReverseConvertible</Type>
                        <Identifiers>
                            <Identifier scheme="ISIN">XX1234567890</Identifier>
                        </Identifiers>
                    </Product>
                </InstrumentMessage>
            </Envelope>
            """);

        // Act
        var message = await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        Assert.Equal("3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10", message.MessageId);
        Assert.Equal("A wrapped product", message.ProductName);
        Assert.Equal("XX1234567890", message.Isin);
        Assert.Equal("ISSUED", Assert.Single(message.Events).EventType);
    }

    [Fact]
    public async Task ParseAsync_WhenMessageIdIsMissing_Throws()
    {
        // Arrange
        await using var xml = StreamOf("""
            <InstrumentMessage xmlns="urn:eventprocessor:instrument-message:v1">
                <Header>
                    <Sender>EXAMPLE-ISSUER</Sender>
                </Header>
            </InstrumentMessage>
            """);

        // Act
        var parse = async () => await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<InvalidDataException>(parse);
        Assert.Contains("MessageId", error.Message);
    }

    [Fact]
    public async Task ParseAsync_WhenTheRootIsNotAnInstrumentMessage_Throws()
    {
        // Arrange
        await using var xml = StreamOf("""
            <SomethingElse xmlns="urn:eventprocessor:instrument-message:v1">
                <Header><MessageId>3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10</MessageId></Header>
            </SomethingElse>
            """);

        // Act
        var parse = async () => await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        var error = await Assert.ThrowsAsync<InvalidDataException>(parse);
        Assert.Contains("InstrumentMessage", error.Message);
    }

    [Fact]
    public async Task ParseAsync_WhenAnElementIsEmpty_ReadsItAsNotSetRatherThanBlank()
    {
        // Arrange
        await using var xml = StreamOf("""
            <InstrumentMessage xmlns="urn:eventprocessor:instrument-message:v1">
                <Header>
                    <MessageId>3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10</MessageId>
                </Header>
                <Product>
                    <Name/>
                    <Type>ReverseConvertible</Type>
                </Product>
            </InstrumentMessage>
            """);

        // Act
        var message = await new MessageParser().ParseAsync(xml, CancellationToken.None);

        // Assert
        Assert.Null(message.ProductName);
        Assert.Null(message.Isin);
        Assert.Equal("ReverseConvertible", message.ProductType);
    }
}
