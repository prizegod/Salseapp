using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace ShopSaleAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ShopSaleDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public class LoginModel
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        public class RegisterModel
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string? FullName { get; set; }
            public string? StoreName { get; set; }
            public string? Email { get; set; }
            public string? Role { get; set; }
        }

        // -------------------------------------------------------------------
        // 🔑 LOGIN API (SQLite DB Check -> Fallback to .env Admin)
        // -------------------------------------------------------------------
        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { message = "Username and Password are required." });
            }

            string userId = "101";
            string username = model.Username;
            string role = "Agent";
            bool isValid = false;

            // 🎯 A. SQLite Database Check (Normal Agents / Users)
            var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower());

            if (dbUser != null)
            {
                try
                {
                    isValid = BCrypt.Net.BCrypt.Verify(model.Password, dbUser.Password);
                }
                catch
                {
                    isValid = (dbUser.Password == model.Password);
                }

                if (isValid)
                {
                    userId = dbUser.Id.ToString();
                    username = dbUser.Username;
                    role = string.IsNullOrWhiteSpace(dbUser.Role) ? "Agent" : dbUser.Role;
                }
            }

            // 🎯 B. Fallback to .env / AppSettings Check (For Admin)
            if (!isValid)
            {
                var envAdminUser = Environment.GetEnvironmentVariable("ADMIN_USERNAME") 
                                   ?? _configuration["ADMIN_USERNAME"] 
                                   ?? "admin";
                var envAdminPass = Environment.GetEnvironmentVariable("ADMIN_PASSWORD") 
                                   ?? _configuration["ADMIN_PASSWORD"] 
                                   ?? "admin123";

                if (model.Username.ToLower() == envAdminUser.ToLower() && model.Password == envAdminPass)
                {
                    isValid = true;
                    userId = "1";
                    username = envAdminUser;
                    role = "Admin";
                }
            }

            if (!isValid)
            {
                return Unauthorized(new { message = "Invalid Username or Password." });
            }

            // 🎯 C. JWT Token Generation
            var secretKey = Environment.GetEnvironmentVariable("JWT_KEY") 
                            ?? _configuration["Jwt:Key"] 
                            ?? "ShopShail_Super_Secret_Key_2026_For_Security_Purpose";
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(8),
                Issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") ?? "ShopShailAPI",
                Audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") ?? "ShopShailClient",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return Ok(new
            {
                token = tokenHandler.WriteToken(token),
                user = new
                {
                    id = userId,
                    username = username,
                    role = role
                }
            });
        }

        // -------------------------------------------------------------------
        // 📝 REGISTER API (Sign Up-க்கு அவசியமானது)
        // -------------------------------------------------------------------
        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest(new { message = "Username and Password are required." });
            }

            // 1. பயனர் பெயர் ஏற்கனவே உள்ளதா எனச் சரிபார்த்தல்
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == model.Username.ToLower());
            if (existingUser != null)
            {
                return BadRequest(new { message = "Username already exists." });
            }

            // 2. புதிய பயனரை SQLite Database-இல் சேமித்தல் (Role: Agent)
            var newUser = new User
            {
                Username = model.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = string.IsNullOrWhiteSpace(model.Role) ? "Agent" : model.Role
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            return Ok(new { message = "User registered successfully." });
        }
    }
}