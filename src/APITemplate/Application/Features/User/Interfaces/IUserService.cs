using APITemplate.Application.Common.DTOs;
using APITemplate.Application.Features.User.DTOs;

namespace APITemplate.Application.Features.User.Interfaces;

public interface IUserService
{
    Task<UserResponse?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PagedResponse<UserResponse>> GetPagedAsync(UserFilter filter, CancellationToken ct = default);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);
    Task ActivateAsync(Guid id, CancellationToken ct = default);
    Task DeactivateAsync(Guid id, CancellationToken ct = default);
    Task ChangeRoleAsync(Guid id, ChangeUserRoleRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
