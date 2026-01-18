using Microsoft.AspNetCore.Identity;

namespace RivianMate.Core.Entities;

/// <summary>
/// Application user extending ASP.NET Core Identity
/// </summary>
public class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Display name shown in the UI
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// When the account was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last successful login time
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<RivianAccount> RivianAccounts { get; set; } = new List<RivianAccount>();
}
