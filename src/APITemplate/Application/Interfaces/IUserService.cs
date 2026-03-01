namespace APITemplate.Application.Interfaces;

public interface IUserService
{
    Task<bool> ValidateAsync(string username, string password, CancellationToken ct = default);
}
