namespace IBankingTuition.UserService.Models;

public class User 
{
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public decimal Balance { get; set; }
    public int RowVersion { get; set; } // Enforces our Optimistic Concurrency Control (OCC)
}