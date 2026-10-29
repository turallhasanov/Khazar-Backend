using Khazar.Domain.Common;

namespace Khazar.Domain.Entities;

/// <summary>A message sent through the storefront contact form (/elaqe), answered by the team by email.</summary>
public class ContactMessage : BaseEntity
{
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 256;
    public const int MaxMessageLength = 4000;

    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string Message { get; private set; } = default!;

    /// <summary>When an admin opened or marked the message as read; null while it is unread.</summary>
    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt is not null;

    // Required by ORMs
    private ContactMessage() { }

    public ContactMessage(string name, string email, string message)
    {
        Name = Guard.MaxLength(Guard.NotEmpty(name, nameof(Name)), MaxNameLength, nameof(Name));
        Email = Guard.MaxLength(Guard.Email(email, nameof(Email)), MaxEmailLength, nameof(Email));
        Message = Guard.MaxLength(Guard.NotEmpty(message, nameof(Message)), MaxMessageLength, nameof(Message));
    }

    /// <summary>Marks the message read or unread. Returns false when it already was.</summary>
    public bool SetRead(bool read, DateTime now)
    {
        if (read == IsRead)
            return false;

        ReadAt = read ? now : null;
        MarkUpdated();
        return true;
    }
}
