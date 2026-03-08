namespace APITemplate.Application.Common.Options;

public sealed class ObservabilityOptions
{
    public string ServiceName { get; init; } = "APITemplate";

    public OtlpEndpointOptions Otlp { get; init; } = new();

    public AspireEndpointOptions Aspire { get; init; } = new();

    public ObservabilityExportersOptions Exporters { get; init; } = new();
}

public sealed class OtlpEndpointOptions
{
    public string Endpoint { get; init; } = string.Empty;
}

public sealed class AspireEndpointOptions
{
    public string Endpoint { get; init; } = string.Empty;
}

public sealed class ObservabilityExportersOptions
{
    public ObservabilityExporterToggleOptions Aspire { get; init; } = new();

    public ObservabilityExporterToggleOptions Otlp { get; init; } = new();

    public ObservabilityExporterToggleOptions Console { get; init; } = new();
}

public sealed class ObservabilityExporterToggleOptions
{
    public bool? Enabled { get; init; }
}
