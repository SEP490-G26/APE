using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/admin/wallet-packages")]
[Authorize(Roles = "Admin")]
public class AdminTopupPackagesController : ControllerBase
{
    private readonly ITopupPackageRepository _topupPackageRepository;

    public AdminTopupPackagesController(ITopupPackageRepository topupPackageRepository)
    {
        _topupPackageRepository = topupPackageRepository;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<TopupPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await _topupPackageRepository.ListAsync(includeInactive: true, cancellationToken);
        return Ok(ApiResponse.Ok(items.Select(MapPackage).ToList()));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TopupPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] UpsertTopupPackageRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var normalized = NormalizeRequest(request);
            var existing = await _topupPackageRepository.GetByCodeAsync(normalized.Code, cancellationToken);
            if (existing is not null)
            {
                return BadRequest(ApiResponse.Fail("A wallet package with this code already exists."));
            }

            var duplicateAmount = await _topupPackageRepository.GetByAmountAsync(normalized.AmountVnd, cancellationToken);
            if (duplicateAmount is not null)
            {
                return BadRequest(ApiResponse.Fail("A wallet package with this amount already exists."));
            }

            var entity = new TopupPackage
            {
                Code = normalized.Code,
                Name = normalized.Name,
                Description = normalized.Description,
                AmountVnd = normalized.AmountVnd,
                SortOrder = normalized.SortOrder,
                IsActive = normalized.IsActive,
                IsFeatured = normalized.IsFeatured,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _topupPackageRepository.CreateAsync(entity, cancellationToken);
            return Ok(ApiResponse.Ok(MapPackage(entity)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ApiResponse<TopupPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(string id, [FromBody] UpsertTopupPackageRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var entity = await _topupPackageRepository.GetByIdAsync(id, cancellationToken);
            if (entity is null)
            {
                return NotFound(ApiResponse.Fail("Top-up package not found."));
            }

            var normalized = NormalizeRequest(request);
            var duplicateCode = await _topupPackageRepository.GetByCodeAsync(normalized.Code, cancellationToken);
            if (duplicateCode is not null && !string.Equals(duplicateCode.Id, entity.Id, StringComparison.Ordinal))
            {
                return BadRequest(ApiResponse.Fail("A wallet package with this code already exists."));
            }

            var duplicateAmount = await _topupPackageRepository.GetByAmountAsync(normalized.AmountVnd, cancellationToken);
            if (duplicateAmount is not null && !string.Equals(duplicateAmount.Id, entity.Id, StringComparison.Ordinal))
            {
                return BadRequest(ApiResponse.Fail("A wallet package with this amount already exists."));
            }

            entity.Code = normalized.Code;
            entity.Name = normalized.Name;
            entity.Description = normalized.Description;
            entity.AmountVnd = normalized.AmountVnd;
            entity.SortOrder = normalized.SortOrder;
            entity.IsActive = normalized.IsActive;
            entity.IsFeatured = normalized.IsFeatured;
            entity.UpdatedAt = DateTime.UtcNow;

            await _topupPackageRepository.UpdateAsync(entity, cancellationToken);
            return Ok(ApiResponse.Ok(MapPackage(entity)));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ApiResponse<TopupPackageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(string id, [FromBody] UpdateTopupPackageStatusRequestDto request, CancellationToken cancellationToken)
    {
        var entity = await _topupPackageRepository.GetByIdAsync(id, cancellationToken);
        if (entity is null)
        {
            return NotFound(ApiResponse.Fail("Top-up package not found."));
        }

        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        await _topupPackageRepository.UpdateAsync(entity, cancellationToken);
        return Ok(ApiResponse.Ok(MapPackage(entity)));
    }

    private static UpsertTopupPackageRequestDto NormalizeRequest(UpsertTopupPackageRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            throw new InvalidOperationException("Code is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Name is required.");
        }

        if (request.AmountVnd < WalletTopupService.MinTopupAmountVnd || request.AmountVnd > WalletTopupService.MaxTopupAmountVnd)
        {
            throw new InvalidOperationException(
                $"AmountVnd must be between {WalletTopupService.MinTopupAmountVnd:N0} and {WalletTopupService.MaxTopupAmountVnd:N0}.");
        }

        return new UpsertTopupPackageRequestDto
        {
            Code = request.Code.Trim().ToUpperInvariant(),
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            AmountVnd = request.AmountVnd,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            IsFeatured = request.IsFeatured
        };
    }

    private static TopupPackageDto MapPackage(TopupPackage item)
    {
        return new TopupPackageDto
        {
            Id = item.Id,
            Code = item.Code,
            Name = item.Name,
            Description = item.Description,
            AmountVnd = item.AmountVnd,
            SortOrder = item.SortOrder,
            IsActive = item.IsActive,
            IsFeatured = item.IsFeatured
        };
    }
}
