using Application.Common;
using Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

[ApiController]
[Route("api/payments/payos")]
[AllowAnonymous]
[Produces("application/json")]
public class PaymentWebhookController : ControllerBase
{
    private readonly WalletTopupService _walletTopupService;

    public PaymentWebhookController(WalletTopupService walletTopupService)
    {
        _walletTopupService = walletTopupService;
    }

    [HttpPost("webhook")]
    public async Task<IActionResult> ReceiveWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawPayload = await reader.ReadToEndAsync(cancellationToken);

        var handled = await _walletTopupService.HandleWebhookAsync(rawPayload, cancellationToken);
        return Ok(ApiResponse.Ok(new { handled }));
    }
}
