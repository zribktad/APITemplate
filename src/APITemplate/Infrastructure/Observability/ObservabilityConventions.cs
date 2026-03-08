namespace APITemplate.Infrastructure.Observability;

public static class ObservabilityConventions
{
    public const string ActivitySourceName = "APITemplate";
    public const string MeterName = "APITemplate";
    public const string HealthMeterName = "APITemplate.Health";
}

public static class TelemetryMetricNames
{
    public const string RateLimitRejections = "apitemplate_rate_limit_rejections";
    public const string HandledExceptions = "apitemplate_exceptions_handled";
    public const string OutputCacheInvalidations = "apitemplate_output_cache_invalidations";
    public const string OutputCacheInvalidationDuration = "apitemplate_output_cache_invalidation_duration";
    public const string OutputCacheOutcomes = "apitemplate_output_cache_outcomes";
    public const string GraphQlRequests = "apitemplate_graphql_requests";
    public const string GraphQlErrors = "apitemplate_graphql_errors";
    public const string GraphQlDocumentCacheHits = "apitemplate_graphql_document_cache_hits";
    public const string GraphQlDocumentCacheMisses = "apitemplate_graphql_document_cache_misses";
    public const string GraphQlOperationCacheHits = "apitemplate_graphql_operation_cache_hits";
    public const string GraphQlOperationCacheMisses = "apitemplate_graphql_operation_cache_misses";
    public const string GraphQlRequestDuration = "apitemplate_graphql_request_duration";
    public const string GraphQlOperationCost = "apitemplate_graphql_operation_cost";
    public const string HealthStatus = "apitemplate_healthcheck_status";
    public const string AuthFailures = "apitemplate_auth_failures";
    public const string ValidationRequestsRejected = "apitemplate_validation_requests_rejected";
    public const string ValidationErrors = "apitemplate_validation_errors";
    public const string ConcurrencyConflicts = "apitemplate_concurrency_conflicts";
    public const string DomainConflicts = "apitemplate_domain_conflicts";
}

public static class TelemetryTagKeys
{
    public const string ApiSurface = "apitemplate.api.surface";
    public const string Authenticated = "apitemplate.authenticated";
    public const string AuthScheme = "auth.scheme";
    public const string AuthFailureReason = "auth.failure_reason";
    public const string CacheOutcome = "cache.outcome";
    public const string CachePolicy = "cache.policy";
    public const string CacheTag = "cache.tag";
    public const string DbSystem = "db.system";
    public const string DbOperation = "db.operation";
    public const string DbStoredProcedureName = "db.stored_procedure.name";
    public const string DbResultCount = "db.result_count";
    public const string ErrorCode = "error.code";
    public const string ExceptionType = "exception.type";
    public const string GraphQlCostKind = "graphql.cost.kind";
    public const string GraphQlHasErrors = "graphql.has_errors";
    public const string GraphQlOperationType = "graphql.operation.type";
    public const string GraphQlPhase = "graphql.phase";
    public const string HttpMethod = "http.request.method";
    public const string HttpResponseStatusCode = "http.response.status_code";
    public const string HttpRoute = "http.route";
    public const string RateLimitPolicy = "rate_limit.policy";
    public const string RetryAttempt = "retry.attempt";
    public const string RetryMax = "retry.max";
    public const string Service = "service";
    public const string StartupComponent = "startup.component";
    public const string StartupMaxRetries = "startup.max_retries";
    public const string StartupRetryCount = "startup.retry_count";
    public const string StartupStep = "startup.step";
    public const string StartupSuccess = "startup.success";
    public const string ValidationDtoType = "validation.dto_type";
    public const string ValidationProperty = "validation.property";
}

public static class TelemetryActivityNames
{
    public const string OutputCacheInvalidate = "output_cache.invalidate";
    public const string CookieSessionRefresh = "auth.cookie-session-refresh";
    public const string RedirectToLogin = "auth.redirect-to-login";
    public const string TokenValidated = "auth.token-validated";

    public static string Startup(string step) => $"startup.{step}";

    public static string StoredProcedure(string operation) => $"stored_procedure.{operation}";
}

public static class TelemetryOutcomeValues
{
    public const string Hit = "hit";
    public const string Store = "store";
    public const string Bypass = "bypass";
}

public static class TelemetryFailureReasons
{
    public const string MissingRefreshToken = "missing_refresh_token";
    public const string MissingTenantClaim = "missing_tenant_claim";
    public const string RefreshFailed = "refresh_failed";
    public const string TokenEndpointRejected = "token_endpoint_rejected";
    public const string TokenRefreshException = "token_refresh_exception";
    public const string UnauthorizedRedirect = "unauthorized_redirect";
}

public static class TelemetrySurfaces
{
    public const string Bff = "bff";
    public const string Documentation = "documentation";
    public const string GraphQl = "graphql";
    public const string Health = "health";
    public const string Rest = "rest";
}

public static class TelemetryDefaults
{
    public const string AspireOtlpEndpoint = "http://localhost:4317";
    public const string Default = "default";
    public const string Sql = "sql";
    public const string Unknown = "unknown";
}

public static class TelemetryEventNames
{
    public const string Retry = "retry";
}

public static class TelemetryContextKeys
{
    public const string OutputCachePolicyName = "OutputCachePolicyName";
}

public static class TelemetryPathPrefixes
{
    public const string GraphQl = "/graphql";
    public const string Health = "/health";
    public const string OpenApi = "/openapi";
    public const string Scalar = "/scalar";
}

public static class TelemetryGraphQlValues
{
    public const string FieldCostKind = "field";
    public const string RequestPhase = "request";
    public const string ResolverPhase = "resolver";
    public const string SyntaxPhase = "syntax";
    public const string TypeCostKind = "type";
    public const string ValidationPhase = "validation";
}

public static class TelemetryStartupSteps
{
    public const string Migrate = "migrate";
    public const string SeedAuthBootstrap = "seed-auth-bootstrap";
    public const string WaitKeycloakReady = "wait-keycloak-ready";
}

public static class TelemetryStartupComponents
{
    public const string AuthBootstrap = "auth-bootstrap";
    public const string Keycloak = "keycloak";
    public const string MongoDb = "mongodb";
    public const string PostgreSql = "postgresql";
}

public static class TelemetryDatabaseSystems
{
    public const string MongoDb = "mongodb";
    public const string PostgreSql = "postgresql";
}

public static class TelemetryStoredProcedureOperations
{
    public const string Execute = "execute";
    public const string QueryFirst = "query-first";
    public const string QueryMany = "query-many";
}

public static class TelemetryMeterNames
{
    public const string AspNetCoreAuthentication = "Microsoft.AspNetCore.Authentication";
    public const string AspNetCoreAuthorization = "Microsoft.AspNetCore.Authorization";
    public const string AspNetCoreConnections = "Microsoft.AspNetCore.Http.Connections";
    public const string AspNetCoreDiagnostics = "Microsoft.AspNetCore.Diagnostics";
    public const string AspNetCoreHosting = "Microsoft.AspNetCore.Hosting";
    public const string AspNetCoreRateLimiting = "Microsoft.AspNetCore.RateLimiting";
    public const string AspNetCoreRouting = "Microsoft.AspNetCore.Routing";
    public const string AspNetCoreServerKestrel = "Microsoft.AspNetCore.Server.Kestrel";
}

public static class TelemetryInstrumentNames
{
    public const string HttpClientRequestDuration = "http.client.request.duration";
    public const string HttpServerRequestDuration = "http.server.request.duration";
}

public static class TelemetryResourceAttributeKeys
{
    public const string DeploymentEnvironmentName = "deployment.environment.name";
    public const string ServiceInstanceId = "service.instance.id";
    public const string ServiceName = "service.name";
    public const string ServiceVersion = "service.version";
}

public static class TelemetryActivitySources
{
    public const string MongoDbDriverDiagnosticSources = "MongoDB.Driver.Core.Extensions.DiagnosticSources";
}
