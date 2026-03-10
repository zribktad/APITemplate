using APITemplate.Application.Features.Category.Mappings;
using APITemplate.Application.Features.Category.Specifications;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using MediatR;
using CategoryEntity = APITemplate.Domain.Entities.Category;

namespace APITemplate.Application.Features.Category;

public sealed record GetCategoriesQuery(CategoryFilter Filter) : IRequest<PagedResponse<CategoryResponse>>;

public sealed record GetCategoryByIdQuery(Guid Id) : IRequest<CategoryResponse?>;

public sealed record GetCategoryStatsQuery(Guid Id) : IRequest<ProductCategoryStatsResponse?>;

public sealed record CreateCategoryCommand(CreateCategoryRequest Request) : IRequest<CategoryResponse>;

public sealed record UpdateCategoryCommand(Guid Id, UpdateCategoryRequest Request) : IRequest;

public sealed record DeleteCategoryCommand(Guid Id) : IRequest;

public sealed class CategoryRequestHandlers :
    IRequestHandler<GetCategoriesQuery, PagedResponse<CategoryResponse>>,
    IRequestHandler<GetCategoryByIdQuery, CategoryResponse?>,
    IRequestHandler<GetCategoryStatsQuery, ProductCategoryStatsResponse?>,
    IRequestHandler<CreateCategoryCommand, CategoryResponse>,
    IRequestHandler<UpdateCategoryCommand>,
    IRequestHandler<DeleteCategoryCommand>
{
    private readonly ICategoryRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryRequestHandlers(ICategoryRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResponse<CategoryResponse>> Handle(GetCategoriesQuery request, CancellationToken ct)
    {
        var items = await _repository.ListAsync(new CategorySpecification(request.Filter), ct);
        var totalCount = await _repository.CountAsync(new CategoryCountSpecification(request.Filter), ct);
        return new PagedResponse<CategoryResponse>(items, totalCount, request.Filter.PageNumber, request.Filter.PageSize);
    }

    public async Task<CategoryResponse?> Handle(GetCategoryByIdQuery request, CancellationToken ct)
        => await _repository.FirstOrDefaultAsync(new CategoryByIdSpecification(request.Id), ct);

    public async Task<ProductCategoryStatsResponse?> Handle(GetCategoryStatsQuery request, CancellationToken ct)
    {
        var stats = await _repository.GetStatsByIdAsync(request.Id, ct);
        return stats?.ToResponse();
    }

    public async Task<CategoryResponse> Handle(CreateCategoryCommand command, CancellationToken ct)
    {
        var category = await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            var entity = new CategoryEntity
            {
                Id = Guid.NewGuid(),
                Name = command.Request.Name,
                Description = command.Request.Description
            };

            await _repository.AddAsync(entity, ct);
            return entity;
        }, ct);

        return category.ToResponse();
    }

    public async Task Handle(UpdateCategoryCommand command, CancellationToken ct)
    {
        var category = await _repository.GetByIdAsync(command.Id, ct)
            ?? throw new NotFoundException(
                nameof(CategoryEntity),
                command.Id,
                ErrorCatalog.Categories.NotFound);

        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            category.Name = command.Request.Name;
            category.Description = command.Request.Description;

            await _repository.UpdateAsync(category, ct);
        }, ct);
    }

    public async Task Handle(DeleteCategoryCommand command, CancellationToken ct)
    {
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            await _repository.DeleteAsync(command.Id, ct, ErrorCatalog.Categories.NotFound);
        }, ct);
    }
}
