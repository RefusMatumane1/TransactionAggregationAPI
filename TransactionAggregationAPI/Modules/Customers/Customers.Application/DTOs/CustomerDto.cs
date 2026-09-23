namespace Modules.Customers.Application.DTOs
{
    public record CustomerDto(
        Guid Id,
        string Email,
        string Name,
        DateTime CreatedAt,
        DateTime? UpdatedAt = null);
}
