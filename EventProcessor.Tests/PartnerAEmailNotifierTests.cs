using Microsoft.Extensions.Options;
using NSubstitute;
using EventProcessor.Abstractions;
using EventProcessor.Model;
using EventProcessor.Partners;

namespace EventProcessor.Tests;

public class PartnerAEmailNotifierTests
{
    private const string Recipient = "partner-a@example.com";

    private static readonly InstrumentMessage Message = new(
        "3f6c2a9e-8b41-4d7a-9c05-6e2f1b7d4a10",
        "8.00% p.a. Barrier Reverse Convertible on Example Corp, Sample Industries",
        "ReverseConvertible",
        "XX1234567890",
        []);

    private static PartnerAEmailNotifier CreateNotifier(IEmailSender emailSender) =>
        new(emailSender, Options.Create(new PartnerAOptions { Recipient = Recipient }));

    [Fact]
    public void ShouldHandle_WhateverTheEventType_ReturnsTrue()
    {
        // Arrange
        var notifier = CreateNotifier(Substitute.For<IEmailSender>());

        // Act
        var handlesIssued = notifier.ShouldHandle(Message, new InstrumentEvent("ISSUED"));
        var handlesOther = notifier.ShouldHandle(Message, new InstrumentEvent("REPRICED"));

        // Assert
        Assert.True(handlesIssued);
        Assert.True(handlesOther);
    }

    [Fact]
    public async Task NotifyAsync_WhenAnEventArrives_PutsTheFourRequestedFieldsInTheBody()
    {
        // Arrange
        var emailSender = Substitute.For<IEmailSender>();
        var notifier = CreateNotifier(emailSender);

        // Act
        await notifier.NotifyAsync(Message, new InstrumentEvent("ISSUED"), CancellationToken.None);

        // Assert
        var body = (string)emailSender.ReceivedCalls().Single().GetArguments()[2]!;

        Assert.Contains("Product name: 8.00% p.a. Barrier Reverse Convertible on Example Corp, Sample Industries", body);
        Assert.Contains("Product type: ReverseConvertible", body);
        Assert.Contains("Event type: ISSUED", body);
        Assert.Contains("ISIN: XX1234567890", body);
    }

    [Fact]
    public async Task NotifyAsync_WhenAnEventArrives_SendsToTheConfiguredRecipient()
    {
        // Arrange
        var emailSender = Substitute.For<IEmailSender>();
        var notifier = CreateNotifier(emailSender);

        // Act
        await notifier.NotifyAsync(Message, new InstrumentEvent("ISSUED"), CancellationToken.None);

        // Assert
        var arguments = emailSender.ReceivedCalls().Single().GetArguments();

        Assert.Equal(Recipient, (string)arguments[0]!);
        Assert.Contains("ISSUED", (string)arguments[1]!);
    }
}
