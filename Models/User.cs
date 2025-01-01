using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace UserService.Models
{
    public class User
    {
        [Key] 
        [Required]
        public int Id { get; set; }
        
        [Required]
        public required string  KeycloakId { get; set; }
        

        [Required]
        public required string  Name { get; set; }

        [Required]
        public DateTime Created { get; set; }
    }
}