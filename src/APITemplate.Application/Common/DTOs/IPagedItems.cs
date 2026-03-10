namespace APITemplate.Application.Common.DTOs;

public interface IPagedItems<T>
{
    PagedResponse<T> Page { get; }
}
