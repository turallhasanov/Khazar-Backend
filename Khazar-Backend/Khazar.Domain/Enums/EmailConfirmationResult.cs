namespace Khazar.Domain.Enums;

public enum EmailConfirmationResult
{
    Confirmed,
    AlreadyConfirmed,
    InvalidCode,
    Expired,
    TooManyAttempts,
}
