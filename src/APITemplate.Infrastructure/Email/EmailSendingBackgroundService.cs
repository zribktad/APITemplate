using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Resilience;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly.Registry;

namespace APITemplate.Infrastructure.Email;

public sealed class EmailSendingBackgroundService : BackgroundService
{
    private readonly ChannelEmailQueue _queue;
    private readonly IEmailSender _sender;
    private readonly ResiliencePipelineProvider<string> _resiliencePipelineProvider;
    private readonly ILogger<EmailSendingBackgroundService> _logger;

    public EmailSendingBackgroundService(
        ChannelEmailQueue queue,
        IEmailSender sender,
        ResiliencePipelineProvider<string> resiliencePipelineProvider,
        ILogger<EmailSendingBackgroundService> logger
    )
    {
        _queue = queue;
        _sender = sender;
        _resiliencePipelineProvider = resiliencePipelineProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pipeline = _resiliencePipelineProvider.GetPipeline(ResiliencePipelineKeys.SmtpSend);

        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await pipeline.ExecuteAsync(
                    async token =>
                    {
                        await _sender.SendAsync(message, token);
                    },
                    stoppingToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send email to {Recipient} with subject '{Subject}' after all retry attempts.",
                    message.To,
                    message.Subject
                );
            }
        }
    }
}
