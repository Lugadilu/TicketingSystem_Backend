using System.Text.Json;

namespace TicketingSystem.Api.Services;

public interface IMpesaDarajaService
{
    Task<JsonElement> StartStkPushAsync(
        string phoneNumber,
        int amount,
        string accountReference,
        string transactionDescription,
        CancellationToken cancellationToken = default);

    Task<JsonElement> QueryStkPushAsync(
        string checkoutRequestId,
        CancellationToken cancellationToken = default);
}