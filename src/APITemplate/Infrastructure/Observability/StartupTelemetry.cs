using System.Diagnostics;

namespace APITemplate.Infrastructure.Observability;

public static class StartupTelemetry
{
    private static readonly ActivitySource ActivitySource = new(ObservabilityConventions.ActivitySourceName);

    public static Task RunRelationalMigrationAsync(Func<Task> action)
        => RunStepAsync(
            TelemetryStartupSteps.Migrate,
            TelemetryStartupComponents.PostgreSql,
            action,
            TelemetryDatabaseSystems.PostgreSql);

    public static Task RunMongoMigrationAsync(Func<Task> action)
        => RunStepAsync(
            TelemetryStartupSteps.Migrate,
            TelemetryStartupComponents.MongoDb,
            action,
            TelemetryDatabaseSystems.MongoDb);

    public static Task RunAuthBootstrapSeedAsync(Func<Task> action)
        => RunStepAsync(
            TelemetryStartupSteps.SeedAuthBootstrap,
            TelemetryStartupComponents.AuthBootstrap,
            action);

    public static Task WaitForKeycloakReadinessAsync(
        int maxRetries,
        Func<int, Task<bool>> attempt,
        Func<Exception> createFailureException)
        => RunRetryingStepAsync(
            TelemetryStartupSteps.WaitKeycloakReady,
            TelemetryStartupComponents.Keycloak,
            maxRetries,
            attempt,
            createFailureException);

    private static Task RunStepAsync(string step, string component, Func<Task> action, string? dbSystem = null)
        => RunStepCoreAsync(step, component, action, dbSystem);

    private static Task RunRetryingStepAsync(
        string step,
        string component,
        int maxRetries,
        Func<int, Task<bool>> attempt,
        Func<Exception>? createFailureException)
        => RunRetryingStepCoreAsync(step, component, maxRetries, attempt, createFailureException);

    private static async Task RunStepCoreAsync(string step, string component, Func<Task> action, string? dbSystem)
    {
        using var activity = StartActivity(step, component);
        if (!string.IsNullOrWhiteSpace(dbSystem))
            activity?.SetTag(TelemetryTagKeys.DbSystem, dbSystem);

        try
        {
            await action();
            activity?.SetTag(TelemetryTagKeys.StartupSuccess, true);
        }
        catch (Exception ex)
        {
            MarkFailure(activity, ex);
            throw;
        }
    }

    private static async Task RunRetryingStepCoreAsync(
        string step,
        string component,
        int maxRetries,
        Func<int, Task<bool>> attempt,
        Func<Exception>? createFailureException)
    {
        using var activity = StartActivity(step, component);
        activity?.SetTag(TelemetryTagKeys.StartupMaxRetries, maxRetries);

        try
        {
            for (var i = 1; i <= maxRetries; i++)
            {
                AddRetryEvent(activity, i, maxRetries);
                if (await attempt(i))
                {
                    activity?.SetTag(TelemetryTagKeys.StartupRetryCount, i);
                    activity?.SetTag(TelemetryTagKeys.StartupSuccess, true);
                    return;
                }
            }

            activity?.SetTag(TelemetryTagKeys.StartupRetryCount, maxRetries);
            throw createFailureException?.Invoke()
                ?? new InvalidOperationException("Startup step failed after exhausting retries.");
        }
        catch (Exception ex)
        {
            MarkFailure(activity, ex);
            throw;
        }
    }

    private static Activity? StartActivity(string step, string component)
    {
        var activity = ActivitySource.StartActivity(TelemetryActivityNames.Startup(step), ActivityKind.Internal);
        activity?.SetTag(TelemetryTagKeys.StartupStep, step);
        activity?.SetTag(TelemetryTagKeys.StartupComponent, component);
        return activity;
    }

    private static void MarkFailure(Activity? activity, Exception exception)
    {
        if (activity is null)
            return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag(TelemetryTagKeys.StartupSuccess, false);
        activity.SetTag(TelemetryTagKeys.ExceptionType, exception.GetType().Name);
    }

    private static void AddRetryEvent(Activity? activity, int attempt, int maxRetries)
    {
        activity?.AddEvent(new ActivityEvent(
            TelemetryEventNames.Retry,
            tags: new ActivityTagsCollection
            {
                { TelemetryTagKeys.RetryAttempt, attempt },
                { TelemetryTagKeys.RetryMax, maxRetries }
            }));
    }
}
