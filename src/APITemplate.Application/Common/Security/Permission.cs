using System.Reflection;

namespace APITemplate.Application.Common.Security;

public static class Permission
{
    public static class Products
    {
        public const string Read = "Products.Read";
        public const string Create = "Products.Create";
        public const string Update = "Products.Update";
        public const string Delete = "Products.Delete";
    }

    public static class Categories
    {
        public const string Read = "Categories.Read";
        public const string Create = "Categories.Create";
        public const string Update = "Categories.Update";
        public const string Delete = "Categories.Delete";
    }

    public static class ProductReviews
    {
        public const string Read = "ProductReviews.Read";
        public const string Create = "ProductReviews.Create";
        public const string Delete = "ProductReviews.Delete";
    }

    public static class ProductData
    {
        public const string Read = "ProductData.Read";
        public const string Create = "ProductData.Create";
        public const string Delete = "ProductData.Delete";
    }

    public static class Users
    {
        public const string Read = "Users.Read";
        public const string Create = "Users.Create";
        public const string Update = "Users.Update";
        public const string Delete = "Users.Delete";
    }

    private static readonly Lazy<IReadOnlySet<string>> LazyAll = new(() =>
    {
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var nestedType in typeof(Permission).GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            foreach (var field in nestedType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (field.IsLiteral && field.FieldType == typeof(string) &&
                    field.GetRawConstantValue() is string value)
                {
                    permissions.Add(value);
                }
            }
        }
        return permissions;
    });

    public static IReadOnlySet<string> All => LazyAll.Value;
}
