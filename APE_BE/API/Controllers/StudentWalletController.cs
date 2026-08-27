using System.Security.Claims;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/student/wallet")]
[Authorize(Roles = "Student")]
[Produces("application/json")]
public class StudentWalletController : ControllerBase
{
    private readonly WalletTopupService _walletTopupService;
    private readonly IAIVndBillingTransactionRepository _aiVndBillingTransactionRepository;

    public StudentWalletController(
        WalletTopupService walletTopupService,
        IAIVndBillingTransactionRepository aiVndBillingTransactionRepository)
    {
        _walletTopupService = walletTopupService;
        _aiVndBillingTransactionRepository = aiVndBillingTransactionRepository;
    }

    [HttpGet("topups/constraints")]
    [ProducesResponseType(typeof(ApiResponse<WalletTopupConstraintsDto>), StatusCodes.Status200OK)]
    public IActionResult GetConstraints()
    {
        return Ok(ApiResponse.Ok(_walletTopupService.GetConstraints()));
    }

    [HttpGet("topups/packages")]
    [ProducesResponseType(typeof(ApiResponse<List<TopupPackageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPackages(CancellationToken cancellationToken)
    {
        var result = await _walletTopupService.GetPackagesAsync(cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("topups/create")]
    [ProducesResponseType(typeof(ApiResponse<WalletTopupSessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateTopup([FromBody] CreateWalletTopupRequestDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Unauthorized(ApiResponse.Fail("Your session has expired."));
            }

            var result = await _walletTopupService.CreateTopupAsync(userId, request, cancellationToken);
            return Ok(ApiResponse.Ok(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("topups/history")]
    [ProducesResponseType(typeof(ApiResponse<List<WalletPaymentHistoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse.Fail("Your session has expired."));
        }

        var result = await _walletTopupService.GetHistoryAsync(userId, status, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("topups/{paymentId}")]
    [ProducesResponseType(typeof(ApiResponse<WalletPaymentHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(string paymentId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse.Fail("Your session has expired."));
        }

        var result = await _walletTopupService.GetByIdAsync(userId, paymentId, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse.Fail("Transaction not found."));
        }

        return Ok(ApiResponse.Ok(result));
    }

    [HttpPost("topups/{paymentId}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<WalletPaymentHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(string paymentId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse.Fail("Your session has expired."));
        }

        var result = await _walletTopupService.CancelAsync(userId, paymentId, cancellationToken);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("ai-transactions")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResult<AIVndBillingTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAiTransactions(
        [FromQuery] string? featureKey,
        [FromQuery] string? status,
        [FromQuery] string? sourceEntityType,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse.Fail("Your session has expired."));
        }

        var (items, total) = await _aiVndBillingTransactionRepository.ListAsync(
            userId,
            featureKey,
            status,
            sourceEntityType,
            fromDate,
            toDate,
            page,
            limit,
            cancellationToken);

        var result = new PaginatedResult<AIVndBillingTransactionDto>
        {
            Items = items.Select(item => new AIVndBillingTransactionDto
            {
                Id = item.Id,
                UserId = item.UserId,
                FeatureKey = item.FeatureKey,
                SourceEntityType = item.SourceEntityType,
                SourceEntityId = item.SourceEntityId,
                Status = item.Status,
                ReportedCostUsd = item.ReportedCostUsd,
                UsdToVndRate = item.UsdToVndRate,
                ChargeMultiplier = item.ChargeMultiplier,
                MinimumBalanceVnd = item.MinimumBalanceVnd,
                ActualCostVnd = item.ActualCostVnd,
                ChargedVnd = item.ChargedVnd,
                ActualDeductedVnd = item.ActualDeductedVnd,
                AbsorbedVnd = item.AbsorbedVnd,
                RefundedVnd = item.RefundedVnd,
                BalanceBeforeVnd = item.BalanceBeforeVnd,
                BalanceAfterVnd = item.BalanceAfterVnd,
                RefundReason = item.RefundReason,
                UsageLogIds = item.UsageLogIds ?? new List<string>(),
                PolicySettingName = item.PolicySettingName,
                PolicyVersion = item.PolicyVersion,
                CreatedBy = item.CreatedBy,
                Notes = item.Notes,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt,
                PolicySnapshot = item.PolicySnapshot,
                UsageSnapshot = item.UsageSnapshot,
                ChargeBreakdown = item.ChargeBreakdown
            }).ToList(),
            Total = total,
            Page = Math.Max(1, page),
            Limit = Math.Max(1, limit),
            TotalPages = (total + Math.Max(1, limit) - 1L) / Math.Max(1, limit)
        };

        return Ok(ApiResponse.Ok(result));
    }
}
