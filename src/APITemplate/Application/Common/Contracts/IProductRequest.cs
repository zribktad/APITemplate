namespace APITemplate.Application.Common.Contracts;
public interface IProductRequest
{
    string Name { get; }
    string? Description { get; }
    decimal Price { get; }
}
