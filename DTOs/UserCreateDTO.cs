using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs
{
    public class UserCreateDTO
    {
        [Required]
        public required string KeycloakId { get; set; }
        
        [Required]
        public required string Name { get; set; }
        
    }
}