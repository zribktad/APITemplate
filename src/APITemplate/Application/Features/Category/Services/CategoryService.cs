using APITemplate.Application.Features.Category.Mappings;
using CategoryEntity = APITemplate.Domain.Entities.Category;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;

namespace APITemplate.Application.Features.Category.Services;
public sealed class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(ICategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<CategoryResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _repository.ListAsync(ct);
        return categories.Select(c => c.ToResponse()).ToList();
    }

    public async Task<CategoryResponse?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(id, ct);
        return category?.ToResponse();
    }

    public async Task<CategoryResponse> CreateAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var entity = new CategoryEntity
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                Description = request.Description
            };

            await _repository.AddAsync(entity, ct);
            return entity;
        }, ct);

        return category.ToResponse();
    }

    public async Task UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        var category = await _repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(
                nameof(CategoryEntity),
                id,
                ErrorCatalog.Categories.NotFound);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            category.Name = request.Name;
            category.Description = request.Description;

            await _repository.UpdateAsync(category, ct);
        }, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _repository.DeleteAsync(id, ct, ErrorCatalog.Categories.NotFound);
        }, ct);
    }

    public async Task<ProductCategoryStatsResponse?> GetStatsAsync(Guid id, CancellationToken ct = default)
    {
        var stats = await _repository.GetStatsByIdAsync(id, ct);
        return stats?.ToResponse();
    }
}
