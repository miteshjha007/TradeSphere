using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TradeSphere.Application.Common.Interfaces;

namespace TradeSphere.Infrastructure.Identity
{
    // Temporary JSON-based implementation for testing
    // TODO: Replace with proper database implementation when migrating to pgAdmin
    public class JsonAuthService : IAuthService
    {
        private readonly IConfiguration _configuration;
        private readonly string _jsonFilePath;

        public JsonAuthService(IConfiguration configuration)
        {
            _configuration = configuration;
            // Get the path to the JSON database file
            _jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "db.json");
        }

        public async Task<string> LoginAsync(string username, string password)
        {
            var db = await LoadDatabaseAsync();
            
            var user = db.Users.FirstOrDefault(u => u.Username == username && u.Password == password);
            if (user == null)
            {
                return null;
            }

            return GenerateJwtToken(user.Id, user.Username, user.Role);
        }

        public async Task<string> RegisterAsync(string username, string email, string password, string role)
        {
            var db = await LoadDatabaseAsync();

            if (db.Users.Any(u => u.Username == username))
                throw new Exception("Username already exists");

            if (db.Users.Any(u => u.Email == email))
                throw new Exception("Email already exists");

            var newUser = new JsonUser
            {
                Id = db.Metadata.LastUserId + 1,
                Username = username,
                Email = email,
                Password = password, // Plain text for testing only!
                Role = role ?? "User",
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(newUser);
            db.Metadata.LastUserId = newUser.Id;

            await SaveDatabaseAsync(db);

            return GenerateJwtToken(newUser.Id, newUser.Username, newUser.Role);
        }

        public async Task<bool> ForgotPasswordAsync(string email)
        {
            var db = await LoadDatabaseAsync();
            
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                return false; // Email not found
            }

            // In a real implementation, generate and send OTP via email
            // For testing, the OTP is already in the database
            return true;
        }

        public async Task<bool> VerifyOtpAsync(string email, string otp)
        {
            var db = await LoadDatabaseAsync();
            
            var otpCode = db.OtpCodes.FirstOrDefault(o => 
                o.Email == email && 
                o.Otp == otp && 
                !o.IsUsed && 
                DateTime.Parse(o.ExpiresAt) > DateTime.UtcNow);

            return otpCode != null;
        }

        public async Task<bool> ResetPasswordAsync(string email, string otp, string newPassword)
        {
            var db = await LoadDatabaseAsync();
            
            // Verify OTP first
            var otpCode = db.OtpCodes.FirstOrDefault(o => 
                o.Email == email && 
                o.Otp == otp && 
                !o.IsUsed && 
                DateTime.Parse(o.ExpiresAt) > DateTime.UtcNow);

            if (otpCode == null)
            {
                return false;
            }

            // Find user and update password
            var user = db.Users.FirstOrDefault(u => u.Email == email);
            if (user == null)
            {
                return false;
            }

            user.Password = newPassword; // Plain text for testing only!
            otpCode.IsUsed = true; // Mark OTP as used

            await SaveDatabaseAsync(db);
            return true;
        }

        private async Task<JsonDatabase> LoadDatabaseAsync()
        {
            if (!File.Exists(_jsonFilePath))
            {
                throw new FileNotFoundException($"Database file not found at: {_jsonFilePath}");
            }

            var jsonContent = await File.ReadAllTextAsync(_jsonFilePath);
            return JsonSerializer.Deserialize<JsonDatabase>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private async Task SaveDatabaseAsync(JsonDatabase db)
        {
            var jsonContent = JsonSerializer.Serialize(db, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(_jsonFilePath, jsonContent);
        }

        private string GenerateJwtToken(int userId, string username, string role)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_configuration["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Role, role)
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        // JSON Data Models
        private class JsonDatabase
        {
            public List<JsonUser> Users { get; set; }
            public List<JsonOtpCode> OtpCodes { get; set; }
            public JsonMetadata Metadata { get; set; }
        }

        private class JsonUser
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string Email { get; set; }
            public string Password { get; set; }
            public string Role { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private class JsonOtpCode
        {
            public string Email { get; set; }
            public string Otp { get; set; }
            public string ExpiresAt { get; set; }
            public bool IsUsed { get; set; }
        }

        private class JsonMetadata
        {
            public int LastUserId { get; set; }
            public string Version { get; set; }
            public string Notes { get; set; }
        }
    }
}
