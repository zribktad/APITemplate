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

        using var provider = services.BuildServiceProvider();

        var sut = new ValidationBehavior<CreateWidgetCommand, string>(
            provider,
            []);

        var act = () => sut.Handle(
            new CreateWidgetCommand(new CreateWidgetRequest(string.Empty)),
            _ => Task.FromResult("ok"),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<APITemplate.Domain.Exceptions.ValidationException>(act);
    }

    [Fact]
    public async Task Handle_WhenRequestIsValid_InvokesNextDelegate()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<CreateWidgetRequest>, CreateWidgetRequestValidator>();

        using var provider = services.BuildServiceProvider();

        var sut = new ValidationBehavior<CreateWidgetCommand, string>(
            provider,
            []);

        var wasCalled = false;
        var result = await sut.Handle(
            new CreateWidgetCommand(new CreateWidgetRequest("widget")),
            _ =>
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

    [Fact]
    public async Task Handle_WhenCollectionItemIsInvalid_ThrowsValidationException()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<OrderLineRequest>, OrderLineRequestValidator>();

        using var provider = services.BuildServiceProvider();

        var sut = new ValidationBehavior<CreateOrderCommand, string>(
            provider,
            []);

        var act = () => sut.Handle(
            new CreateOrderCommand([new OrderLineRequest(string.Empty), new OrderLineRequest("valid")]),
            _ => Task.FromResult("ok"),
            TestContext.Current.CancellationToken);

        await Should.ThrowAsync<APITemplate.Domain.Exceptions.ValidationException>(act);
    }

    [Fact]
    public async Task Handle_WhenCollectionItemsAreValid_InvokesNextDelegate()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<OrderLineRequest>, OrderLineRequestValidator>();

        using var provider = services.BuildServiceProvider();

        var sut = new ValidationBehavior<CreateOrderCommand, string>(
            provider,
            []);

        var wasCalled = false;
        var result = await sut.Handle(
            new CreateOrderCommand([new OrderLineRequest("one"), new OrderLineRequest("two")]),
            _ =>
            {
                wasCalled = true;
                return Task.FromResult("ok");
            },
            TestContext.Current.CancellationToken);

        wasCalled.ShouldBeTrue();
        result.ShouldBe("ok");
    }

    private sealed record OrderLineRequest(string Name);

    private sealed record CreateOrderCommand(IReadOnlyCollection<OrderLineRequest> Lines) : IRequest<string>;

    private sealed class OrderLineRequestValidator : AbstractValidator<OrderLineRequest>
    {
        public OrderLineRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
