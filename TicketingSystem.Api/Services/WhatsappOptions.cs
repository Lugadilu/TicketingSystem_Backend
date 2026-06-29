namespace TicketingSystem.Api.Services;

public sealed class WhatsAppOptions
{
    public string AccessToken { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
    public string GraphApiVersion { get; set; } = "v18.0";
}