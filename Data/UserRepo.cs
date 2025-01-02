using UserService.Models;

namespace UserService.Data
{
    public class UserRepo : IUserRepo
    {
        private readonly AppDbContext _context;

        public UserRepo(AppDbContext context)
        {
            _context = context;
        }

        public User GetUserByKeycloakId(string Keycloakid)
        {
            var user = _context.Users.FirstOrDefault(p => p.KeycloakId == Keycloakid);
            if (user == null)
            {
                throw new InvalidOperationException($"User with Keycloakid {Keycloakid} was not found.");
            }
    
            return user;
        }

        public void CreateUser(User user)
        {
            if(user == null)
            {
                throw new ArgumentNullException(nameof(user));
            }

            _context.Users.Add(user);
        }

        public void UpdateUser(User user)
        {
            throw new NotImplementedException();
        }

        public void DeleteUser(User user)
        {
            _context.Users.Remove(GetUserById(user.Id));
        }

        public Task DeleteUserFromKeycloakAsync(User user)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<User> GetAllUsers()
        {
            return _context.Users.ToList();
        }

        public User GetUserById(int id)
        {
            var user = _context.Users.FirstOrDefault(p => p.Id == id);
            if (user == null)
            {
                throw new InvalidOperationException($"User with id {id} was not found.");
            }
    
            return user;
        }

        public bool SaveChanges()
        {
            return (_context.SaveChanges() >= 0);
        }
    }
}