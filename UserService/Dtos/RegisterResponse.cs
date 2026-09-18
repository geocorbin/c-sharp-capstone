namespace UserService.Dtos;

public class RegisterResponse
{
    public required Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string Role { get; set; }
    public required string MembershipStatus { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required string Message { get; set; }
}