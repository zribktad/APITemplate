using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Security;
using APITemplate.Application.Features.User;
using APITemplate.Application.Features.User.DTOs;
using APITemplate.Application.Features.User.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public class UserRequestHandlersTests
{
    private readonly Mock<IUserRepository> _repositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IPublisher> _publisherMock;
    private readonly Mock<ILogger<UserRequestHandlers>> _loggerMock;
    private readonly UserRequestHandlers _sut;

    public UserRequestHandlersTests()
    {
        _repositoryMock = new Mock<IUserRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _publisherMock = new Mock<IPublisher>();
        _loggerMock = new Mock<ILogger<UserRequestHandlers>>();

        _passwordHasherMock.Setup(h => h.Hash(It.IsAny<string>())).Returns("hashed_password");

        _sut = new UserRequestHandlers(
            _repositoryMock.Object,
            _passwordHasherMock.Object,
            _unitOfWorkMock.Object,
            _publisherMock.Object,
            _loggerMock.Object
        );
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_WhenUserExists_ReturnsUserResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var expected = new UserResponse(
            Guid.NewGuid(),
            "testuser",
            "test@example.com",
            true,
            UserRole.User,
            DateTime.UtcNow
        );

        _repositoryMock
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expected);

        var result = await _sut.Handle(new GetUserByIdQuery(expected.Id), ct);

        result.ShouldNotBeNull();
        result!.Id.ShouldBe(expected.Id);
        result.Username.ShouldBe("testuser");
    }

    [Fact]
    public async Task GetByIdAsync_WhenUserDoesNotExist_ReturnsNull()
    {
        _repositoryMock
            .Setup(r =>
                r.FirstOrDefaultAsync(
                    It.IsAny<UserByIdSpecification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((UserResponse?)null);

        var result = await _sut.Handle(
            new GetUserByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        result.ShouldBeNull();
    }

    // --- GetPagedAsync ---

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResponse()
    {
        var ct = TestContext.Current.CancellationToken;
        var filter = new UserFilter(PageNumber: 1, PageSize: 10);
        var items = new List<UserResponse>
        {
            new(Guid.NewGuid(), "user1", "user1@test.com", true, UserRole.User, DateTime.UtcNow),
            new(
                Guid.NewGuid(),
                "user2",
                "user2@test.com",
                true,
                UserRole.PlatformAdmin,
                DateTime.UtcNow
            ),
        };

        _repositoryMock
            .Setup(r =>
                r.ListAsync(It.IsAny<UserFilterSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(items);
        _repositoryMock
            .Setup(r =>
                r.CountAsync(It.IsAny<UserCountSpecification>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(2);

        var result = await _sut.Handle(new GetUsersQuery(filter), ct);

        result.Items.Count().ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.PageNumber.ShouldBe(1);
        result.PageSize.ShouldBe(10);
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ReturnsCreatedUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var request = new CreateUserRequest("newuser", "new@example.com", "Password1!");

        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(request.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser u, CancellationToken _) => u);

        var result = await _sut.Handle(new CreateUserCommand(request), ct);

        result.Username.ShouldBe("newuser");
        result.Email.ShouldBe("new@example.com");
        result.Id.ShouldNotBe(Guid.Empty);
        result.IsActive.ShouldBeTrue();
        result.Role.ShouldBe(UserRole.User);

        _passwordHasherMock.Verify(h => h.Hash("Password1!"), Times.Once);
        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<AppUser>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<UserRegisteredNotification>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateAsync_WhenEmailExists_ThrowsConflictException()
    {
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("existing@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () =>
            _sut.Handle(
                new CreateUserCommand(
                    new CreateUserRequest("user", "existing@test.com", "Password1!")
                ),
                TestContext.Current.CancellationToken
            );

        var ex = await Should.ThrowAsync<ConflictException>(act);
        ex.ErrorCode.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);
    }

    [Fact]
    public async Task CreateAsync_WhenUsernameExists_ThrowsConflictException()
    {
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () =>
            _sut.Handle(
                new CreateUserCommand(
                    new CreateUserRequest("existinguser", "new@test.com", "Password1!")
                ),
                TestContext.Current.CancellationToken
            );

        var ex = await Should.ThrowAsync<ConflictException>(act);
        ex.ErrorCode.ShouldBe(ErrorCatalog.Users.UsernameAlreadyExists);
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_WhenUserExists_UpdatesFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var user = CreateTestUser();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("updated@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repositoryMock
            .Setup(r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _sut.Handle(
            new UpdateUserCommand(
                user.Id,
                new UpdateUserRequest("updateduser", "updated@test.com")
            ),
            ct
        );

        user.Username.ShouldBe("updateduser");
        user.Email.ShouldBe("updated@test.com");
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenSameEmailAndUsername_SkipsUniquenessCheck()
    {
        var user = CreateTestUser();

        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.Handle(
            new UpdateUserCommand(user.Id, new UpdateUserRequest(user.Username, user.Email)),
            TestContext.Current.CancellationToken
        );

        _repositoryMock.Verify(
            r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
        _repositoryMock.Verify(
            r => r.ExistsByUsernameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task UpdateAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var act = () =>
            _sut.Handle(
                new UpdateUserCommand(Guid.NewGuid(), new UpdateUserRequest("name", "e@e.com")),
                TestContext.Current.CancellationToken
            );

        await Should.ThrowAsync<NotFoundException>(act);
    }

    [Fact]
    public async Task UpdateAsync_WhenNewEmailExists_ThrowsConflictException()
    {
        var user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _repositoryMock
            .Setup(r => r.ExistsByEmailAsync("taken@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () =>
            _sut.Handle(
                new UpdateUserCommand(
                    user.Id,
                    new UpdateUserRequest(user.Username, "taken@test.com")
                ),
                TestContext.Current.CancellationToken
            );

        var ex = await Should.ThrowAsync<ConflictException>(act);
        ex.ErrorCode.ShouldBe(ErrorCatalog.Users.EmailAlreadyExists);
    }

    // --- ActivateAsync / DeactivateAsync ---

    [Fact]
    public async Task ActivateAsync_SetsIsActiveToTrue()
    {
        var user = CreateTestUser(isActive: false);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.Handle(new ActivateUserCommand(user.Id), TestContext.Current.CancellationToken);

        user.IsActive.ShouldBeTrue();
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeactivateAsync_SetsIsActiveToFalse()
    {
        var user = CreateTestUser(isActive: true);
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.Handle(
            new DeactivateUserCommand(user.Id),
            TestContext.Current.CancellationToken
        );

        user.IsActive.ShouldBeFalse();
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ActivateAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var act = () =>
            _sut.Handle(
                new ActivateUserCommand(Guid.NewGuid()),
                TestContext.Current.CancellationToken
            );

        await Should.ThrowAsync<NotFoundException>(act);
    }

    // --- ChangeRoleAsync ---

    [Fact]
    public async Task ChangeRoleAsync_ChangesUserRole()
    {
        var user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.Handle(
            new ChangeUserRoleCommand(user.Id, new ChangeUserRoleRequest(UserRole.PlatformAdmin)),
            TestContext.Current.CancellationToken
        );

        user.Role.ShouldBe(UserRole.PlatformAdmin);
        _repositoryMock.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _publisherMock.Verify(
            p => p.Publish(It.IsAny<UserRoleChangedNotification>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ChangeRoleAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var act = () =>
            _sut.Handle(
                new ChangeUserRoleCommand(
                    Guid.NewGuid(),
                    new ChangeUserRoleRequest(UserRole.PlatformAdmin)
                ),
                TestContext.Current.CancellationToken
            );

        await Should.ThrowAsync<NotFoundException>(act);
    }

    // --- DeleteAsync ---

    [Fact]
    public async Task DeleteAsync_CallsRepositoryDeleteAndCommits()
    {
        var user = CreateTestUser();
        _repositoryMock
            .Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await _sut.Handle(new DeleteUserCommand(user.Id), TestContext.Current.CancellationToken);

        _repositoryMock.Verify(r => r.DeleteAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWorkMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenUserNotFound_ThrowsNotFoundException()
    {
        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppUser?)null);

        var act = () =>
            _sut.Handle(
                new DeleteUserCommand(Guid.NewGuid()),
                TestContext.Current.CancellationToken
            );

        await Should.ThrowAsync<NotFoundException>(act);
    }

    // --- Helpers ---

    private static AppUser CreateTestUser(bool isActive = true, UserRole role = UserRole.User)
    {
        return new AppUser
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            NormalizedUsername = AppUser.NormalizeUsername("testuser"),
            Email = "test@example.com",
            NormalizedEmail = AppUser.NormalizeEmail("test@example.com"),
            PasswordHash = "hashed",
            IsActive = isActive,
            Role = role,
            TenantId = Guid.NewGuid(),
            Audit = new() { CreatedAtUtc = DateTime.UtcNow },
        };
    }
}
