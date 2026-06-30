namespace TicketingSystem.Api.Services;

public sealed class WhatsAppOptions
{
     public string ApiBaseUrl { get; set; } = "https://graph.facebook.com/v25.0";

    public string AccessToken { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string PhoneNumberId { get; set; } = string.Empty;
    public string BusinessAccountId { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;

    public string TestNumber { get; set; } = string.Empty;

    public int ConversationTimeoutMinutes { get; set; } = 30;
    public int AgentHandoffTimeoutMinutes { get; set; } = 120;
}