Event Processor
===============

A console app that reads an XML message about a financial instrument, records its events with a timestamp, and forwards each event to any partner interested in it.

Built with .NET 10.

How to run it
-------------
```
dotnet run --project EventProcessor
```

To run the tests:
```
dotnet test
```

The app reads the sample message, writes the event through the repository, and notifies both partners. Everything it does is visible in the console output.

Partner B's file is written to: `EventProcessor/bin/Debug/net10.0/out/partner-b/`
A run against the supplied sample produces this:
```xml
  <InstrumentNotification>
    <Timespan>2026-08-22T07:07:10.8070000+00:00</Timespan>
    <ISIN>XX1234567890</ISIN>
  </InstrumentNotification>
```

The message format is a simple XML schema (`urn:eventprocessor:instrument-message:v1`), see `EventProcessor/Samples/sample-message.xml`. All identifiers in the sample are fictional.

The following are configured in appsettings.json:

- `Source:File:Path`: which message to read
- `Email:Provider`: Logging or Smtp
- `Email:Smtp:*`: host, port, from, credentials, used when Provider is Smtp
- `Partners:A:Recipient`: where Partner A's mail goes
- `Partners:B:EventType`: the event type Partner B cares about
- `Partners:B:OutputDirectory`: where Partner B's file is written

`Email:Provider` defaults to Logging so the app runs with no mail server to hand. You can set it to Smtp to send for real.

Structure
---------
The solution consists of two projects:
- `EventProcessor`: the console app
- `EventProcessor.Tests`: xUnit tests

The interfaces of the app:
- `IMessageSource`: gets the raw stream for a message from wherever it lives
- `IMessageParser`: parses the source stream to internal model
- `IEventRepository`: saves the events, and checks whether a message has already been processed
- `IPartnerNotifier`: hands the model to one partner (different implementation per Partner)
- `IEmailSender`: sends a composed mail (console today, SMTP available)

Mermaid Diagram:
```mermaid
  flowchart TD
      A["IMessageSource<br/>reads the XML"] --> B["IMessageParser<br/>XML to InstrumentMessage"]
      B --> C{"Seen this MessageId before?"}
      C -->|yes| D["Skip the message"]
      C -->|no| E["IEventRepository<br/>save every event in one transaction"]
      E --> F{"For each event,<br/>which partners want it?"}
      F --> G["Partner A<br/>always"]
      F --> H["Partner B<br/>only the configured event type"]
      G --> I["IEmailSender<br/>console today, SMTP available"]
      H --> J["InstrumentNotification<br/>XML file on disk"]
  ```

How to add a new partner
--------------------
A notifier class:
```csharp
  public sealed class PartnerCNotifier(IOptions<PartnerCOptions> options) : IPartnerNotifier
  {
      public string PartnerName => "Partner C";

      public bool ShouldHandle(InstrumentMessage message, InstrumentEvent instrumentEvent) =>
          /* whatever Partner C cares about */;

      public Task NotifyAsync(InstrumentMessage message, InstrumentEvent instrumentEvent,
                              CancellationToken cancellationToken) =>
          /* whatever format and transport Partner C wants */;
  }
```

An options class for whatever Partner C needs configured:
```csharp
  public sealed class PartnerCOptions
  {
      public const string SectionName = "Partners:C";

      public string Endpoint { get; set; } = string.Empty;
  }
```

Its settings in appsettings.json:
```json
  "Partners": {
    "C": {
      "Endpoint": "https://partner-c.example.com/notifications"
    }
  }
```

And the registration in Program.cs:
```csharp
  builder.Services.AddSingleton<IPartnerNotifier, PartnerCNotifier>();

  builder.Services
      .AddOptions<PartnerCOptions>()
      .Bind(builder.Configuration.GetSection(PartnerCOptions.SectionName))
      .Validate(o => !string.IsNullOrWhiteSpace(o.Endpoint), "Partners:C:Endpoint must be set.")
      .ValidateOnStart();
```

`ShouldHandle` is on the Partner because it is a Partner-exclusive concern.

Libraries
---------
**App project:**

`Microsoft.Extensions.Hosting`: Needed for generic host, DI, configuration, logging, options.

`System.Xml.Linq`: Built in, used XDocument for parsing.

**Test project:**

`xUnit`: testing framework.

`NSubstitute`: used for mocking.

Notable Tests
-----
36 tests in total. The most important ones are:

- the parser takes the product ISIN even when an underlying's ISIN appears first in the document, which is the case a descendant search gets wrong
- the parser returns XX1234567890 and not either underlying ISIN
- the parser reads every event, not just the first
- Partner B's filter follows configuration rather than a hardcoded event code
- Partner B writes no file when the ISIN is missing, and does not throw
- the handler does nothing when the MessageId was already processed
- the handler saves a message's events in one call, not one call per event
- nothing is sent to a partner if the save fails
- one partner throwing does not stop the others
