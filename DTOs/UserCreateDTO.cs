using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs
{
    public class UserCreateDTO
    {
        [Required]
        public required string Name { get; set; }
        
        [Required]
        public DateTime Created { get; set; }
    }
}