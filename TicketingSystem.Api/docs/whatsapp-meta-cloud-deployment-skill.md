# WhatsApp Meta Cloud Deployment Skill

**Reusable agent skill for ASP.NET Core + Meta WhatsApp Business Cloud API**

Version: 1.0 | Extracted from Millennium EA / Ticketing System integration

---

## 1. Purpose

This document teaches an AI coding agent (or developer) how to deploy the same WhatsApp bot architecture to **any** ASP.NET Core backend — without reinventing patterns each time.

**Use when:** New client WhatsApp bot, redeployment, Meta webhook integration, interactive list menus, phone-based user verification.

---

## 2. Product behavior (Shuru / KRA model)

| When | Who | What happens |
|------|-----|--------------|
| Any message (idle) | Everyone | Welcome text + "View Services" interactive menu |
| User picks a service | Unregistered | "Register on portal first, then message again" |
| User picks a service | Registered | Service runs with real UserId + Role |
| Mid-flow (typing ticket #, description, agent chat) | Registered | Continue that flow — no welcome spam |

**Key rule:** The bot is open to everyone. Registration gates **actions**, not greetings.

---

## 3. Architecture

```
Meta WhatsApp Cloud API
        │
        ▼ POST /api/whatsapp/webhook
WhatsAppWebhookController  ──► validate signature ──► enqueue ──► 200 OK
        │
        ▼
WhatsAppMessageQueue (in-memory Channel)
        │
        ▼
WhatsAppMessageBackgroundWorker (BackgroundService)
        │
        ▼
WhatsAppConversationService (state machine)
        ├── WhatsAppUserResolver (phone → User)
        ├── Domain services (ITicketService, etc.)
        ├── WhatsAppSenderService (outbound to Meta)
        └── WhatsAppLiveChatHub (optional SignalR)
```

**Why a queue?** Meta requires a fast HTTP 200. Ticket creation and DB work happen asynchronously.

---

## 4. Required files (copy per project)

| File | Role |
|------|------|
| WhatsAppOptions.cs | Configuration model |
| WhatsAppWebhookController.cs | Webhook GET verify + POST receive |
| WhatsAppWebhookSignatureValidator.cs | HMAC-SHA256 security |
| WhatsAppIncomingMessage.cs | Internal message record |
| WhatsAppMessageQueue.cs | Channel-based queue |
| WhatsAppMessageBackgroundWorker.cs | Async processor |
| WhatsAppConversationState.cs | Step + UserId + Role in memory |
| WhatsAppUserResolver.cs | Phone-based user lookup |
| WhatsAppConversationService.cs | Conversation brain |
| WhatsAppSenderService.cs | Send text + list menu to Meta |
| WhatsAppWebhookDto.cs | Deserialize Meta JSON |
| WhatsAppSupportController.cs | Optional agent replies |
| WhatsAppLiveChatHub.cs | Optional live chat |

**Shared:** IPhoneNumberService with NormalizeForStorage + PhonesMatch

---

## 5. Program.cs wiring

```csharp
builder.Services.Configure<WhatsAppOptions>(
    builder.Configuration.GetSection("WhatsApp"));
builder.Services.AddMemoryCache();
builder.Services.AddSignalR();

builder.Services.AddHttpClient<IWhatsAppSenderService, WhatsAppSenderService>(
    (sp, client) => {
        var o = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
        client.BaseAddress = new Uri(o.ApiBaseUrl);
    });

builder.Services.AddSingleton<IWhatsAppMessageQueue, WhatsAppMessageQueue>();
builder.Services.AddHostedService<WhatsAppMessageBackgroundWorker>();
builder.Services.AddScoped<IWhatsAppConversationService, WhatsAppConversationService>();
builder.Services.AddScoped<IWhatsAppUserResolver, WhatsAppUserResolver>();
builder.Services.AddScoped<IWhatsAppWebhookSignatureValidator,
    WhatsAppWebhookSignatureValidator>();

app.MapHub<WhatsAppLiveChatHub>("/hubs/whatsapp-live-chat");
```

---

## 6. Configuration (appsettings)

```json
"WhatsApp": {
  "ApiBaseUrl": "https://graph.facebook.com/v25.0",
  "AccessToken": "EAA...",
  "VerifyToken": "your-random-verify-string",
  "PhoneNumberId": "123456789",
  "BusinessAccountId": "...",
  "AppSecret": "...",
  "ConversationTimeoutMinutes": 30
}
```

**Never commit tokens.** Use user secrets or environment variables in production.

---

## 7. Meta Developer Console setup

1. Create Meta app → WhatsApp product
2. Get Phone Number ID, generate permanent Access Token
3. Set webhook callback: `https://YOUR_DOMAIN/api/whatsapp/webhook`
4. Set Verify Token (must match appsettings)
5. Subscribe to `messages` field
6. Dev: use ngrok → `ngrok http 5072`

---

## 8. Conversation logic order

In `HandleIncomingMessageAsync`, check in this order:

1. Invalid phone → error message
2. Step = HumanHandoff → forward to agent / end chat
3. Step = AwaitingAgentIssue → start handoff
4. Step = AwaitingNewTicketDescription → create ticket
5. Step = AwaitingTicketId → show ticket status
6. Text = "end chat" → welcome + menu
7. Text = service action → registration gate → service
8. **Default** → welcome + menu (any other text)

---

## 9. Service menu ids

Interactive list row `id` values become message text when tapped:

| id | Registered user action |
|----|------------------------|
| create_ticket | Ask issue → CreateTicketAsync |
| check_status | Ask ticket # → lookup |
| my_tickets | GetMyTicketsAsync(userId, role) |
| download_report | Placeholder / PDF |
| talk_to_agent | SignalR live chat |

---

## 10. Registration gate (copy this pattern)

```csharp
var user = await _userResolver.ResolveByWhatsAppPhoneAsync(
    message.FromPhoneNumber, cancellationToken);

if (user is null)
{
    await _sender.SendRegistrationRequiredMessageAsync(
        message.FromPhoneNumber, cancellationToken);
    return;
}

SetState(normalizedPhone, nextStep, user.Id, user.Role);
```

Registration message:
> We couldn't find an account for this WhatsApp number. Please register on the portal first, then message us again to continue.

---

## 11. Phone normalization (critical)

| Source | Example |
|--------|---------|
| WhatsApp | 254712345678 |
| User registered as | 0712345678 |

**On save (registration/profile):**
```csharp
PhoneNumber = _phoneNumberService.NormalizeForStorage(dto.PhoneNumber);
```

**On lookup:**
```csharp
_phoneNumberService.PhonesMatch(user.PhoneNumber, whatsappFromPhone);
```

Normalize existing DB rows before go-live.

---

## 12. Welcome message template

Customize per client in WhatsAppSenderService:

```
Welcome to [COMPANY] Service Centre!

We are [one-line description]. You can access the following
services right here on WhatsApp:

• Create Ticket
• Check Status
• My Tickets
• Download Report
• Talk to an Agent

Tap the button below to get started.
```

Then send interactive list with button **"View Services"**.

---

## 13. Test matrix

| Test | Expected |
|------|----------|
| Any idle message | Welcome + menu |
| Unregistered → Create Ticket | Registration message |
| Registered → Create Ticket | Ticket created |
| Registered → My Tickets | User's tickets listed |
| Wrong ticket # for user | Not found |
| Talk to Agent → message | Live chat + ack |
| end chat | Welcome + menu |
| 0712… in DB, 2547… on WhatsApp | Match after normalize |

---

## 14. Agent prompt for new deployment

Paste into Cursor when starting a new WhatsApp project:

```
Use skill: whatsapp-meta-cloud-deployment

Deploy WhatsApp Meta Cloud bot on this ASP.NET Core API:
- Shuru pattern: welcome+menu for all idle messages
- Registration gate on service actions only
- Queue + background worker + webhook signature validation
- Phone normalization on save and lookup
- Integrate with existing domain services (do not duplicate logic)

Client name: [NAME]
Welcome copy: [TEXT]
Portal URL: [URL]
Webhook URL: [URL]
Menu services: [LIST]
```

---

## 15. Skill location (Cursor)

Personal skill path:
`~/.cursor/skills/whatsapp-meta-cloud-deployment/SKILL.md`

Reference:
`~/.cursor/skills/whatsapp-meta-cloud-deployment/reference.md`

---

*End of document*
