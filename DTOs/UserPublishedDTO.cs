namespace UserService.DTOs;

public class UserPublishedDTO
{
    public int Id { get; set; }
    public required string KeycloakId { get; set; }
    public required string Name { get; set; }
    public required string Event { get; set; }
    
}