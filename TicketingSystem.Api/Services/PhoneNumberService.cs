namespace TicketingSystem.Api.Services;

public interface IPhoneNumberService
{
    string NormalizeKenyanMobile(string phoneNumber);
}

public sealed class PhoneNumberService : IPhoneNumberService
{
    public string NormalizeKenyanMobile(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return string.Empty;

        var value = phoneNumber
            .Trim()
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("(", "")
            .Replace(")", "");

        if (value.StartsWith("+"))
            value = value[1..];

        if (value.StartsWith("0") && value.Length == 10)
            value = "254" + value[1..];

        if (value.StartsWith("7") && value.Length == 9)
            value = "254" + value;

        return value;
    }
}