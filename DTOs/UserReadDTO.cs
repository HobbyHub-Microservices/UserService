namespace UserService.DTOs
{
    public class UserReadDTO
    {
        public int Id { get; set; }
        public required string KeycloakId { get; set; }
        public required string Name { get; set; }
        public DateTime Created { get; set; }
    }
}