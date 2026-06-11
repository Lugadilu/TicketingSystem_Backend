using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TicketingSystem.Api.Dtos.Payments;
using TicketingSystem.Api.Services;
using System.Globalization;
using Microsoft.Extensions.Options;

namespace TicketingSystem.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IMpesaDarajaService _mpesaService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IMpesaDarajaService mpesaService,
        ILogger<PaymentsController> logger)
    {
        _mpesaService = mpesaService;
        _logger = logger;
    }

    [HttpPost("stk-push")]
    public async Task<IActionResult> InitiateStkPush(
        [FromBody] StkPushRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mpesaService.StartStkPushAsync(
                dto.PhoneNumber,
                dto.Amount,
                dto.AccountReference,
                dto.TransactionDescription,
                cancellationToken);

            return Ok(new
            {
                Success = true,
                Message = "STK Push initiated successfully",
                Data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "STK Push validation failed");
            return BadRequest(new { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STK Push failed unexpectedly");
            return StatusCode(500, new { Success = false, Message = "Payment service error" });
        }
    }

    [HttpPost("stk-query")]
    public async Task<IActionResult> QueryStkPush(
        [FromBody] StkQueryRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _mpesaService.QueryStkPushAsync(
                dto.CheckoutRequestId,
                cancellationToken);

            return Ok(new
            {
                Success = true,
                Message = "STK Query successful",
                Data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "STK Query validation failed");
            return BadRequest(new { Success = false, Message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "STK Query failed unexpectedly");
            return StatusCode(500, new { Success = false, Message = "Payment service error" });
        }
    }


    [HttpPost("callback")]
    [AllowAnonymous] // Safaricom has no auth token to send
    public IActionResult HandleCallback([FromBody] JsonElement payload)
    {
        _logger.LogInformation("M-Pesa callback received: {Payload}", payload);

        // Extract key fields
        var resultCode = payload.GetProperty("Body")
            .GetProperty("stkCallback")
            .GetProperty("ResultCode")
            .GetInt32();

        var resultDesc = payload.GetProperty("Body")
            .GetProperty("stkCallback")
            .GetProperty("ResultDesc")
            .GetString();

        var checkoutRequestId = payload.GetProperty("Body")
            .GetProperty("stkCallback")
            .GetProperty("CheckoutRequestID")
            .GetString();

        var merchantRequestId = payload.GetProperty("Body")
            .GetProperty("stkCallback")
            .GetProperty("MerchantRequestID")
            .GetString();

        if (resultCode == 0)
        {
            // Payment successful
            var callbackMetadata = payload.GetProperty("Body")
                .GetProperty("stkCallback")
                .GetProperty("CallbackMetadata")
                .GetProperty("Item");

            string? mpesaReceiptNumber = null;
            DateTime? transactionDate = null;
            decimal? amount = null;

            foreach (var item in callbackMetadata.EnumerateArray())
            {
                var name = item.GetProperty("Name").GetString();
                if (item.TryGetProperty("Value", out var value))
                {
                    switch (name)
                    {
                        case "MpesaReceiptNumber":
                            mpesaReceiptNumber = value.GetString();
                            break;
                        case "TransactionDate":
                            var dateStr = value.GetString();
                            if (DateTime.TryParseExact(dateStr, "yyyyMMddHHmmss", null, DateTimeStyles.None, out var dt))
                                transactionDate = dt;
                            break;
                        case "Amount":
                            amount = value.GetDecimal();
                            break;
                    }
                }
            }

            _logger.LogInformation(
                "Payment successful. Receipt: {Receipt}, Amount: {Amount}, Date: {Date}",
                mpesaReceiptNumber, amount, transactionDate);

            // TODO: Update your database here
            // Save payment record, mark order as paid, etc.
        }
        else
        {
            _logger.LogWarning(
                "Payment failed or cancelled. Code: {ResultCode}, Desc: {ResultDesc}",
                resultCode, resultDesc);
        }

        // Always return 200 OK so Safaricom doesn't retry
        return Ok();
    }



[HttpGet("diagnostic")]
[AllowAnonymous]
public IActionResult Diagnostic()
{
    var config = HttpContext.RequestServices
        .GetRequiredService<IOptions<MpesaDarajaOptions>>().Value;

    return Ok(new
    {
        BaseUrl = config.BaseUrl,
        ConsumerKeyLength = config.ConsumerKey?.Length ?? 0,
        ConsumerSecretLength = config.ConsumerSecret?.Length ?? 0,
        PassKeyLength = config.PassKey?.Length ?? 0,
        ShortCode = config.ShortCode,
        CallbackUrl = config.CallbackUrl,
        TransactionType = config.TransactionType
    });
}
}
