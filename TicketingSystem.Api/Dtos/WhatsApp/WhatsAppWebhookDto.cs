using System.Text.Json.Serialization;

namespace TicketingSystem.Api.Dtos.WhatsApp;

public sealed class WhatsAppWebhookDto
{
    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("entry")]
    public List<WhatsAppEntryDto> Entry { get; set; } = new();
}

public sealed class WhatsAppEntryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("changes")]
    public List<WhatsAppChangeDto> Changes { get; set; } = new();
}

public sealed class WhatsAppChangeDto
{
    [JsonPropertyName("field")]
    public string Field { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public WhatsAppValueDto Value { get; set; } = new();
}

public sealed class WhatsAppValueDto
{
    [JsonPropertyName("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;

    [JsonPropertyName("metadata")]
    public WhatsAppMetadataDto Metadata { get; set; } = new();

    [JsonPropertyName("contacts")]
    public List<WhatsAppContactDto>? Contacts { get; set; }

    [JsonPropertyName("messages")]
    public List<WhatsAppMessageDto>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<WhatsAppStatusDto>? Statuses { get; set; }
}

public sealed class WhatsAppMetadataDto
{
    [JsonPropertyName("display_phone_number")]
    public string DisplayPhoneNumber { get; set; } = string.Empty;

    [JsonPropertyName("phone_number_id")]
    public string PhoneNumberId { get; set; } = string.Empty;
}

public sealed class WhatsAppContactDto
{
    [JsonPropertyName("profile")]
    public WhatsAppProfileDto Profile { get; set; } = new();

    [JsonPropertyName("wa_id")]
    public string WaId { get; set; } = string.Empty;
}

public sealed class WhatsAppProfileDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public sealed class WhatsAppMessageDto
{
    [JsonPropertyName("from")]
    public string From { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public WhatsAppTextDto? Text { get; set; }

    [JsonPropertyName("interactive")]
    public WhatsAppInteractiveDto? Interactive { get; set; }
}

public sealed class WhatsAppTextDto
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}

public sealed class WhatsAppInteractiveDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("button_reply")]
    public WhatsAppButtonReplyDto? ButtonReply { get; set; }

    [JsonPropertyName("list_reply")]
    public WhatsAppListReplyDto? ListReply { get; set; }
}

public sealed class WhatsAppButtonReplyDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
}

public sealed class WhatsAppListReplyDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

public sealed class WhatsAppStatusDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonPropertyName("recipient_id")]
    public string RecipientId { get; set; } = string.Empty;
}