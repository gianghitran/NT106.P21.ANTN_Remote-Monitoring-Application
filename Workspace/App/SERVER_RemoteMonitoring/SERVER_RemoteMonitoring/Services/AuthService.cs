using SERVER_RemoteMonitoring.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using System.Windows;

namespace SERVER_RemoteMonitoring.Services
{
    public class AuthService
    {
        private readonly DatabaseService _dbService;

        public AuthService(DatabaseService dbService)
        {
            _dbService = dbService;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var db = _dbService.GetDataBaseConnection();

            var user = await db.Table<Models.User>()
                            .Where(u => u.Username == username).FirstOrDefaultAsync();

            if (user == null)
            {
                return false;
            }

            var isValid = BCrypt.Net.BCrypt.Verify(password, user?.Password); // Verify the password

            MessageBox.Show($"Password: {password} \nHashed Password: {user?.Password} \nIs Valid: {isValid}");

            return isValid;
        }

        public async Task<bool> RegisterAsync(string username, string email, string password)
        {
            var db = _dbService.GetDataBaseConnection();
            var existingUser = await db.Table<Models.User>()
                                        .Where(u => u.Username == username || u.Email == email).FirstOrDefaultAsync();

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password); 

            if (existingUser != null)
            {
                return false; // User already exists
            }

            var newUser = new Models.User
            {
                Username = username,
                Email = email,
                Password = hashedPassword, // Use hashed password for secure
                Role = "User", // Default role
            };
            await db.InsertAsync(newUser);
            return true;
        }

        public async Task<Models.User> GetUserAsync(string username)
        {
            var db = _dbService.GetDataBaseConnection();
            var user = await db.Table<Models.User>()
                            .Where(u => u.Username == username).FirstOrDefaultAsync();
            return user;
        }
    }
}
