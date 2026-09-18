namespace UserService.Models;

public class User : IAuditable
{
    public Guid UserId { get; set; } = Guid.NewGuid();

    public required string Email { get; set; }

    public required string PasswordHash { get; set; }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public required string PhoneNumber { get; set; }

    public Role Role { get; set; } = Role.Patron;

    public MembershipStatus MembershipStatus { get; set; } = MembershipStatus.Active;

    public DateTime? MemberSince { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}