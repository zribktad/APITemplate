namespace APITemplate.Application.Common.Resilience;

public static class ResiliencePipelineKeys
{
    public const string MongoProductDataDelete = "mongo-productdata-delete";
    public const string SmtpSend = "smtp-send";
    public const string KeycloakAdmin = "keycloak-admin";
}
