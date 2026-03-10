namespace APITemplate.Application.Common.Contracts;
public interface ISortableFilter
{
    string? SortBy { get; }
    string? SortDirection { get; }
}
