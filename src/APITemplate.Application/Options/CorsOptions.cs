namespace APITemplate.Application.Options;

public sealed class CorsOptions
{
    public string[] AllowedOrigins { get; init; } = [];
}
