using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using EventProcessor.Abstractions;
using EventProcessor.Email;
using EventProcessor.Handling;
using EventProcessor.Parsing;
using EventProcessor.Partners;
using EventProcessor.Persistence;
using EventProcessor.Sources;

namespace EventProcessor;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });

        builder.Services.AddSingleton<IMessageSource, FileMessageSource>();

        builder.Services
            .AddOptions<FileSourceOptions>()
            .Bind(builder.Configuration.GetSection(FileSourceOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Path), "Source:File:Path must be set.")
            .ValidateOnStart();

        builder.Services.AddSingleton<IMessageParser, MessageParser>();
        builder.Services.AddSingleton<MessageHandler>();

        builder.Services.AddSingleton<IEventRepository, InMemoryEventRepository>();

        if (string.Equals(builder.Configuration["Email:Provider"], "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();

            builder.Services
                .AddOptions<SmtpOptions>()
                .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Host), "Email:Smtp:Host must be set.")
                .Validate(o => !string.IsNullOrWhiteSpace(o.From), "Email:Smtp:From must be set.")
                .ValidateOnStart();
        }
        else
        {
            builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
        }

        // Partner A
        builder.Services.AddSingleton<IPartnerNotifier, PartnerAEmailNotifier>();

        builder.Services
            .AddOptions<PartnerAOptions>()
            .Bind(builder.Configuration.GetSection(PartnerAOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Recipient), "Partners:A:Recipient must be set.")
            .ValidateOnStart();

        // Partner B
        builder.Services.AddSingleton<IPartnerNotifier, PartnerBXmlFileNotifier>();

        builder.Services
            .AddOptions<PartnerBOptions>()
            .Bind(builder.Configuration.GetSection(PartnerBOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.EventType), "Partners:B:EventType must be set.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.OutputDirectory), "Partners:B:OutputDirectory must be set.")
            .ValidateOnStart();

        using var host = builder.Build();

        await host.StartAsync();

        // Cancellation when pressing Ctrl+C, or SIGTERM (in case of docker stop)
        var cancellationToken = host.Services
            .GetRequiredService<IHostApplicationLifetime>()
            .ApplicationStopping;

        var source = host.Services.GetRequiredService<IMessageSource>();
        var parser = host.Services.GetRequiredService<IMessageParser>();
        var handler = host.Services.GetRequiredService<MessageHandler>();

        await foreach (var messageStream in source.ReadAsync(cancellationToken))
        {
            await using (messageStream)
            {
                var message = await parser.ParseAsync(messageStream, cancellationToken);
                await handler.HandleAsync(message, cancellationToken);
            }
        }

        await host.StopAsync();

        return 0;
    }
}
