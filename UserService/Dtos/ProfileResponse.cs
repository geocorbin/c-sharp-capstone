namespace UserService.Dtos;

public class ProfileResponse
{
    public required Guid UserId { get; set; }
    public required string Email { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Role { get; set; }
    public required string MembershipStatus { get; set; }
    public DateTime? MemberSince { get; set; }
    public int ActiveReservations { get; set; }
    public int BorrowingHistory { get; set; }
}