using UserService.Models;

namespace UserService.Data
{
    public interface IUserRepo
    {
        bool SaveChanges();

        IEnumerable<User> GetAllUsers();
        User GetUserById(int id);

        User GetUserByKeycloakId(string Keycloakid);
        void CreateUser(User user);
        
        void UpdateUser(User user); // Optional, depending on EF Core usage
        void DeleteUser(User user);
        
        Task DeleteUserFromKeycloakAsync(User user);
    }
}