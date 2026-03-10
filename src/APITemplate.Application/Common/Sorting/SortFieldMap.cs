using System.Linq.Expressions;
using Ardalis.Specification;

namespace APITemplate.Application.Common.Sorting;
public sealed class SortFieldMap<TEntity>
    where TEntity : class
{
    private readonly record struct Entry(SortField Field, Expression<Func<TEntity, object?>> KeySelector);

    private readonly List<Entry> _entries = [];
    private Expression<Func<TEntity, object?>>? _default;

    public IReadOnlyCollection<string> AllowedNames =>
        _entries.Select(e => e.Field.Value).ToArray();

    public SortFieldMap<TEntity> Add(SortField field, Expression<Func<TEntity, object?>> keySelector)
    {
        _entries.Add(new(field, keySelector));
        return this;
    }

    public SortFieldMap<TEntity> Default(Expression<Func<TEntity, object?>> keySelector)
    {
        _default = keySelector;
        return this;
    }

    public void ApplySort(ISpecificationBuilder<TEntity> query, string? sortBy, string? sortDirection)
    {
        var desc = !string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        var key = _entries.FirstOrDefault(e => e.Field.Matches(sortBy)).KeySelector ?? _default;

        if (key is null) return;

        if (desc)
            query.OrderByDescending(key);
        else
            query.OrderBy(key);
    }
}
