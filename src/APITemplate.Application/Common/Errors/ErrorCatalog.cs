namespace APITemplate.Application.Common.Errors;

public static class ErrorCatalog
{
    public static class General
    {
        public const string Unknown = "GEN-0001";
        public const string ValidationFailed = "GEN-0400";
        public const string NotFound = "GEN-0404";
        public const string Conflict = "GEN-0409";
        public const string ConcurrencyConflict = "GEN-0409-CONCURRENCY";
    }

    public static class Auth
    {
        public const string Forbidden = "AUTH-0403";
    }

    public static class Products
    {
        public const string NotFound = "PRD-0404";
        public const string ProductDataNotFound = "PRD-2404";
    }

    public static class ProductData
    {
        public const string NotFound = "PDT-0404";
        public const string InUse = "PDT-0409";
    }

    public static class Categories
    {
        public const string NotFound = "CAT-0404";
    }

    public static class Reviews
    {
        public const string ProductNotFoundForReview = "REV-2101";
        public const string ReviewNotFound = "REV-0404";
    }

    public static class Users
    {
        public const string NotFound = "USR-0404";
        public const string EmailAlreadyExists = "USR-0409-EMAIL";
        public const string UsernameAlreadyExists = "USR-0409-USERNAME";
    }

    public static class Tenants
    {
        public const string NotFound = "TNT-0404";
        public const string CodeAlreadyExists = "TNT-0409-CODE";
    }

    public static class Invitations
    {
        public const string NotFound = "INV-0404";
        public const string AlreadyPending = "INV-0409-PENDING";
        public const string Expired = "INV-0410";
        public const string AlreadyAccepted = "INV-0409-ACCEPTED";
        public const string NotPending = "INV-0409-NOT-PENDING";
    }
}
