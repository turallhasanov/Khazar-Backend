using Khazar.Application.DTOs.Content;

namespace Khazar.Application.Services;

/// <summary>The storefront contact form (submit) and the admin inbox of its messages.</summary>
public interface IContactMessageAppService
{
    /// <summary>Stores a message from the contact page; field errors use the code "invalid_content".</summary>
    Task<ContactMessageDto> SubmitAsync(CreateContactMessageRequestDto dto, CancellationToken cancellationToken = default);

    /// <summary>Every message, newest first.</summary>
    Task<IReadOnlyList<ContactMessageDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ContactMessageDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ContactMessageDto> SetReadAsync(Guid id, bool isRead, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
