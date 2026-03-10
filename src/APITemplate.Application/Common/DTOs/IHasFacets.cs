namespace APITemplate.Application.Common.DTOs;

public interface IHasFacets<TFacets>
{
    TFacets Facets { get; }
}
