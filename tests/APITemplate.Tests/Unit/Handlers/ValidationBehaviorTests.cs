using APITemplate.Application.Common.Behaviors;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace APITemplate.Tests.Unit.Handlers;

public sealed class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenNestedRequestIsInvalid_ThrowsValidationException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<CreateWidgetRequest>, CreateWidgetRequestValidator>();

        var sut = new ValidationBehavior<CreateWidgetCommand, string>(
            services.BuildServiceProvider(),
            []);

        var act = () => sut.Handle(
            new CreateWidgetCommand(new CreateWidgetRequest(string.Empty)),
            () => Task.FromResult("ok"),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<APITemplate.Domain.Exceptions.ValidationException>(act);
    }

    [Fact]
    public async Task Handle_WhenRequestIsValid_InvokesNextDelegate()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<CreateWidgetRequest>, CreateWidgetRequestValidator>();

        var sut = new ValidationBehavior<CreateWidgetCommand, string>(
            services.BuildServiceProvider(),
            []);

        var wasCalled = false;
        var result = await sut.Handle(
            new CreateWidgetCommand(new CreateWidgetRequest("widget")),
            () =>
            {
                wasCalled = true;
                return Task.FromResult("ok");
            },
            TestContext.Current.CancellationToken);

        wasCalled.ShouldBeTrue();
        result.ShouldBe("ok");
    }

    private sealed record CreateWidgetRequest(string Name);

    private sealed record CreateWidgetCommand(CreateWidgetRequest Request) : IRequest<string>;

    private sealed class CreateWidgetRequestValidator : AbstractValidator<CreateWidgetRequest>
    {
        public CreateWidgetRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
