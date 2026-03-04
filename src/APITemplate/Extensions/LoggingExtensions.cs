using APITemplate.Application.Options;
using APITemplate.Infrastructure.Logging;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;

namespace APITemplate.Extensions;

public static class LoggingExtensions
{
    public static WebApplicationBuilder AddApplicationRedaction(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<RedactionOptions>()
            .Bind(builder.Configuration.GetSection("Redaction"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var redactionOptions = builder.Configuration.GetSection("Redaction").Get<RedactionOptions>() ?? new RedactionOptions();
        var hmacKey = RedactionConfiguration.ResolveHmacKey(
            redactionOptions,
            Environment.GetEnvironmentVariable);

        builder.Services.AddRedaction(redactionBuilder =>
        {
            redactionBuilder.SetRedactor<ErasingRedactor>(LogDataClassifications.Personal);

#pragma warning disable EXTEXP0002 // HMAC redactor API is currently marked experimental in the Microsoft.Extensions.Compliance.Redaction package.
            redactionBuilder.SetHmacRedactor(
                options =>
                {
                    options.KeyId = redactionOptions.KeyId;
                    options.Key = hmacKey;
                },
                new DataClassificationSet(LogDataClassifications.Sensitive));
#pragma warning restore EXTEXP0002

            redactionBuilder.SetFallbackRedactor<ErasingRedactor>();
        });

        builder.Logging.EnableRedaction();

        return builder;
    }
}
