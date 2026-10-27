namespace Khazar.Domain.Enums;

/// <summary>Moderation state of a product review. Only approved reviews are shown on the storefront.</summary>
public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
