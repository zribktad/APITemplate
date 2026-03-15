using APITemplate.Application.Common.Context;
using APITemplate.Application.Common.Email;
using APITemplate.Application.Common.Events;
using APITemplate.Application.Common.Options;
using APITemplate.Application.Features.TenantInvitation.DTOs;
using APITemplate.Application.Features.TenantInvitation.Mappings;
using APITemplate.Application.Features.TenantInvitation.Specifications;
using APITemplate.Domain.Entities;
using APITemplate.Domain.Enums;
using APITemplate.Domain.Exceptions;
using APITemplate.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TenantInvitationEntity = APITemplate.Domain.Entities.TenantInvitation;

namespace APITemplate.Application.Features.TenantInvitation;

public sealed record CreateTenantInvitationCommand(CreateTenantInvitationRequest Request)
    : IRequest<TenantInvitationResponse>;

public sealed record AcceptTenantInvitationCommand(string Token) : IRequest;

public sealed record RevokeTenantInvitationCommand(Guid InvitationId) : IRequest;

public sealed record ResendTenantInvitationCommand(Guid InvitationId) : IRequest;

public sealed record GetTenantInvitationsQuery(TenantInvitationFilter Filter)
    : IRequest<PagedResponse<TenantInvitationResponse>>;

public sealed class TenantInvitationRequestHandlers
    : IRequestHandler<CreateTenantInvitationCommand, TenantInvitationResponse>,
        IRequestHandler<AcceptTenantInvitationCommand>,
        IRequestHandler<RevokeTenantInvitationCommand>,
        IRequestHandler<ResendTenantInvitationCommand>,
        IRequestHandler<GetTenantInvitationsQuery, PagedResponse<TenantInvitationResponse>>
{
    private readonly ITenantInvitationRepository _invitationRepository;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecureTokenGenerator _tokenGenerator;
    private readonly IPublisher _publisher;
    private readonly ITenantProvider _tenantProvider;
    private readonly TimeProvider _timeProvider;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<TenantInvitationRequestHandlers> _logger;

    public TenantInvitationRequestHandlers(
        ITenantInvitationRepository invitationRepository,
        ITenantRepository tenantRepository,
        IUnitOfWork unitOfWork,
        ISecureTokenGenerator tokenGenerator,
        IPublisher publisher,
        ITenantProvider tenantProvider,
        TimeProvider timeProvider,
        IOptions<EmailOptions> emailOptions,
        ILogger<TenantInvitationRequestHandlers> logger
    )
    {
        _invitationRepository = invitationRepository;
        _tenantRepository = tenantRepository;
        _unitOfWork = unitOfWork;
        _tokenGenerator = tokenGenerator;
        _publisher = publisher;
        _tenantProvider = tenantProvider;
        _timeProvider = timeProvider;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<PagedResponse<TenantInvitationResponse>> Handle(
        GetTenantInvitationsQuery request,
        CancellationToken ct
    )
    {
        var items = await _invitationRepository.ListAsync(
            new TenantInvitationFilterSpecification(request.Filter),
            ct
        );
        var totalCount = await _invitationRepository.CountAsync(
            new TenantInvitationCountSpecification(request.Filter),
            ct
        );

        return new PagedResponse<TenantInvitationResponse>(
            items,
            totalCount,
            request.Filter.PageNumber,
            request.Filter.PageSize
        );
    }

    public async Task<TenantInvitationResponse> Handle(
        CreateTenantInvitationCommand command,
        CancellationToken ct
    )
    {
        var normalizedEmail = AppUser.NormalizeEmail(command.Request.Email);

        if (await _invitationRepository.HasPendingInvitationAsync(normalizedEmail, ct))
            throw new ConflictException(
                $"A pending invitation already exists for '{command.Request.Email}'.",
                ErrorCatalog.Invitations.AlreadyPending
            );

        var tenant =
            await _tenantRepository.GetByIdAsync(_tenantProvider.TenantId, ct)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Tenant),
                _tenantProvider.TenantId,
                ErrorCatalog.Tenants.NotFound
            );

        var rawToken = _tokenGenerator.GenerateToken();
        var tokenHash = _tokenGenerator.HashToken(rawToken);

        var invitation = new TenantInvitationEntity
        {
            Id = Guid.NewGuid(),
            Email = command.Request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            TokenHash = tokenHash,
            ExpiresAtUtc = _timeProvider
                .GetUtcNow()
                .UtcDateTime.AddHours(_emailOptions.InvitationTokenExpiryHours),
        };

        await _invitationRepository.AddAsync(invitation, ct);
        await _unitOfWork.CommitAsync(ct);

        try
        {
            await _publisher.Publish(
                new TenantInvitationCreatedNotification(
                    invitation.Id,
                    invitation.Email,
                    tenant.Name,
                    rawToken
                ),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to publish TenantInvitationCreatedNotification for invitation {InvitationId}.", invitation.Id);
        }

        return invitation.ToResponse();
    }

    public async Task Handle(AcceptTenantInvitationCommand command, CancellationToken ct)
    {
        var tokenHash = _tokenGenerator.HashToken(command.Token);
        var invitation =
            await _invitationRepository.GetValidByTokenHashAsync(tokenHash, ct)
            ?? throw new NotFoundException(
                "Invitation not found or expired.",
                ErrorCatalog.Invitations.NotFound
            );

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (invitation.ExpiresAtUtc < now)
            throw new ConflictException(
                "Invitation has expired.",
                ErrorCatalog.Invitations.Expired
            );

        if (invitation.Status == InvitationStatus.Accepted)
            throw new ConflictException(
                "Invitation has already been accepted.",
                ErrorCatalog.Invitations.AlreadyAccepted
            );

        invitation.Status = InvitationStatus.Accepted;
        await _invitationRepository.UpdateAsync(invitation, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task Handle(RevokeTenantInvitationCommand command, CancellationToken ct)
    {
        var invitation =
            await _invitationRepository.GetByIdAsync(command.InvitationId, ct)
            ?? throw new NotFoundException(
                nameof(TenantInvitationEntity),
                command.InvitationId,
                ErrorCatalog.Invitations.NotFound
            );

        invitation.Status = InvitationStatus.Revoked;
        await _invitationRepository.UpdateAsync(invitation, ct);
        await _unitOfWork.CommitAsync(ct);
    }

    public async Task Handle(ResendTenantInvitationCommand command, CancellationToken ct)
    {
        var invitation =
            await _invitationRepository.GetByIdAsync(command.InvitationId, ct)
            ?? throw new NotFoundException(
                nameof(TenantInvitationEntity),
                command.InvitationId,
                ErrorCatalog.Invitations.NotFound
            );

        if (invitation.Status != InvitationStatus.Pending)
            throw new ConflictException(
                "Only pending invitations can be resent.",
                ErrorCatalog.Invitations.NotPending
            );

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (invitation.ExpiresAtUtc < now)
            throw new ConflictException(
                "Invitation has expired. Create a new one instead.",
                ErrorCatalog.Invitations.Expired
            );

        var tenant =
            await _tenantRepository.GetByIdAsync(_tenantProvider.TenantId, ct)
            ?? throw new NotFoundException(
                nameof(Domain.Entities.Tenant),
                _tenantProvider.TenantId,
                ErrorCatalog.Tenants.NotFound
            );

        // Generate new token for resend
        var rawToken = _tokenGenerator.GenerateToken();
        invitation.TokenHash = _tokenGenerator.HashToken(rawToken);

        await _invitationRepository.UpdateAsync(invitation, ct);
        await _unitOfWork.CommitAsync(ct);

        try
        {
            await _publisher.Publish(
                new TenantInvitationCreatedNotification(
                    invitation.Id,
                    invitation.Email,
                    tenant.Name,
                    rawToken
                ),
                ct
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to publish TenantInvitationCreatedNotification for invitation {InvitationId}.", invitation.Id);
        }
    }
}
