namespace TicketingSystem.Api.Dtos.Payments;

public sealed class StkPushRequestDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string AccountReference { get; set; } = string.Empty;
    public string TransactionDescription { get; set; } = string.Empty;
}