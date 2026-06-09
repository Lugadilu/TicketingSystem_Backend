namespace TicketingSystem.Api.Services;

public sealed class FornaxSmsOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string SendPath { get; set; } = "/api/services/sendsms/";
    public string ApiKey { get; set; } = string.Empty;
    public string PartnerId { get; set; } = string.Empty;
    public string Shortcode { get; set; } = string.Empty;
    
    public bool TestMode { get; set; }
    public string TestMobile { get; set; } = string.Empty;
}

// this class is used to bind the Fornax SMS configuration from appsettings.json and user secrets and is injected into the FornaxSmsService to send SMS messages. It contains properties for the base URL, API key, partner ID,
// and shortcode required to authenticate and send SMS messages through the Fornax SMS service.