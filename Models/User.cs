using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace UserService.Models
{
    public class User
    {
        [Key] //For EntityFramework
        [Required]
        public int Id { get; set; }

        [Required]
        public required string  Name { get; set; }

        [Required]
        public DateTime Created { get; set; }
    }
}