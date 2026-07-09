namespace TicketingSystem.Api.Models;

public enum ConversationChannel
{
    WhatsApp = 1
}

public enum ConversationStatus
{
    Open = 1,
    WaitingForAgent = 2,
    Assigned = 3,
    Closed = 4
}

public enum MessageDirection
{
    Inbound = 1,
    Outbound = 2
}

public enum MessageSenderType
{
    Customer = 1,
    Agent = 2,
    Bot = 3,
    System = 4
}