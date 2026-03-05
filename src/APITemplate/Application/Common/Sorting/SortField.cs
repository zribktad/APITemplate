namespace APITemplate.Application.Common.Sorting;
public sealed record SortField(string Value)
{
    public bool Matches(string? input) =>
        string.Equals(Value, input?.Trim(), StringComparison.OrdinalIgnoreCase);
}
