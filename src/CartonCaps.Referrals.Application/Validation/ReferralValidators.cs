using System.Text.RegularExpressions;

namespace CartonCaps.Referrals.Application.Validation;

public static class ReferralValidators
{
    private static readonly Regex EmailRegex =
        new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex =
        new(@"^\+[1-9]\d{7,14}$", RegexOptions.Compiled);

    public static bool IsValidEmail(string value) => EmailRegex.IsMatch(value.Trim());
    public static bool IsValidPhone(string value) => PhoneRegex.IsMatch(value.Trim());

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NormalizePhone(string phone) => phone.Trim();

    public static string NormalizeContact(string contactType, string contactValue)
    {
        return contactType switch
        {
            "email" => NormalizeEmail(contactValue),
            "phone" => NormalizePhone(contactValue),
            _ => contactValue.Trim()
        };
    }
}
