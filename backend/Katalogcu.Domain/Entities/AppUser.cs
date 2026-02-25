using Katalogcu.Domain.Common;

namespace Katalogcu.Domain.Entities
{
    public class AppUser : BaseEntity
    {
        public string FirstName {get;set;} = string.Empty;
        public string LastName {get;set;} = string.Empty;
        public string Email {get;set;} = string.Empty;
        public string PasswordHash {get;set;} = string.Empty;
        public string? PasswordSalt { get; set; }
        public string Role {get;set;} = "Customer";
        public string? CompanyName {get; set;} 
        public string? PhoneNumber { get; set; }
        public int PublicLinkVersion { get; set; } = 1;
        public bool PublicLinkEnabled { get; set; } = true;
    }
}
