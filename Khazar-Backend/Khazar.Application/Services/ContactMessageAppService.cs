using Khazar.Application.Common.Exceptions;
using Khazar.Application.Common.Interfaces;
using Khazar.Application.Common.Mappings;
using Khazar.Application.Content;
using Khazar.Application.DTOs.Content;
using Khazar.Domain.Entities;
using Khazar.Domain.Exceptions;

namespace Khazar.Application.Services;

public sealed class ContactMessageAppService(IContactMessageRepository messageRepository) : IContactMessageAppService
{
    public async Task<ContactMessageDto> SubmitAsync(CreateContactMessageRequestDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var fields = ContentValidator.Normalize(dto);

        ContactMessage message;
        try
        {
            message = new ContactMessage(fields.Name, fields.Email, fields.Message);
        }
        catch (DomainException)
        {
            // The only rule left after the validator is the address format.
            throw new ValidationException("Field 'email' is not a valid email address.", ContentSchema.InvalidContentErrorCode);
        }

        await messageRepository.AddAsync(message, cancellationToken);
        return message.ToDto();
    }

    public async Task<IReadOnlyList<ContactMessageDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await messageRepository.GetAllAsync(cancellationToken)).Select(message => message.ToDto()).ToList();

    public async Task<ContactMessageDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        (await LoadAsync(id, cancellationToken)).ToDto();

    public async Task<ContactMessageDto> SetReadAsync(Guid id, bool isRead, CancellationToken cancellationToken = default)
    {
        var message = await LoadAsync(id, cancellationToken);
        if (message.SetRead(isRead, DateTime.UtcNow))
            await messageRepository.UpdateAsync(message, cancellationToken);
        return message.ToDto();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
        await messageRepository.DeleteAsync(await LoadAsync(id, cancellationToken), cancellationToken);

    private async Task<ContactMessage> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await messageRepository.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(ContactMessage), id);
}
