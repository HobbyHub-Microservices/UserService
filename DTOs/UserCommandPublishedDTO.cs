namespace UserService.DTOs;

public class UserCommandPublishedDto
{
    public required int Id { get; set; }
    public required string Event { get; set; }
}