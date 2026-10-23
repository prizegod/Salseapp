using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopSaleAPI.Data;
using ShopSaleAPI.Models;
using System.Security.Claims;

namespace ShopSaleAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly ShopSaleDbContext _context;

        public UsersController(ShopSaleDbContext context)
        {
            _context = context;
        }

        // 1. PUBLIC SIGNUP: Manager கணக்கு உருவாக்க
        [HttpPost("register-manager")]
        [AllowAnonymous]
        public async Task<IActionResult> RegisterManager([FromBody] RegisterDto model)
        {
            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                return BadRequest(new { message = "Username already exists!" });
            }

            var newManager = new User
            {
                Username = model.Username,
                Password = model.Password,
                Role = "Manager"
            };

            _context.Users.Add(newManager);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Manager account registered successfully!" });
        }

        // 2. CREATE AGENT: Manager தனது Agent-ஐ உருவாக்க
        [HttpPost("create-agent")]
        [Authorize(Roles = "Manager,Admin,Owner")]
        public async Task<IActionResult> CreateAgent([FromBody] RegisterDto model)
        {
            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(currentUserIdStr, out int managerId))
            {
                return Unauthorized(new { message = "Invalid Token. Please login again." });
            }

            if (await _context.Users.AnyAsync(u => u.Username == model.Username))
            {
                return BadRequest(new { message = "Username already exists!" });
            }

            var newAgent = new User
            {
                Username = model.Username,
                Password = model.Password,
                Role = "Agent",
                ManagerId = managerId
            };

            _context.Users.Add(newAgent);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Agent '{model.Username}' created successfully!" });
        }

        // 3. ADMIN HIERARCHY SUMMARY: அட்மின் டேஷ்போர்டுக்காக மட்டும்
        [HttpGet("admin-hierarchy-summary")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> GetAdminHierarchySummary()
        {
            try
            {
                var totalUsers = await _context.Users.CountAsync();
                var totalManagers = await _context.Users.CountAsync(u => u.Role == "Manager");
                var totalAgents = await _context.Users.CountAsync(u => u.Role == "Agent");

                var managerHierarchy = await _context.Users
                .Where(u => u.Role == "Manager")
                .Select(m => new
                {
                    managerId = m.Id,
                    managerName = m.Username,
                    agentsCount = _context.Users.Count(u => u.Role == "Agent" && u.ManagerId == m.Id)
                })
                .ToListAsync();

                return Ok(new
                {
                    totalUsers,
                    totalManagers,
                    totalAgents,
                    managerHierarchy
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // 🎯 4. UPDATE PROFILE: Password மாற்றும் எண்ட்பாயிண்ட் (Model Error Fixed)
        [HttpPut("profile")]
        [Authorize]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId))
            {
                return Unauthorized(new { message = "Invalid Token." });
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            // Password update (Optional)
            if (!string.IsNullOrEmpty(request.NewPassword))
            {
                if (string.IsNullOrEmpty(request.CurrentPassword) || user.Password != request.CurrentPassword)
                {
                    return BadRequest(new { message = "Current password is incorrect." });
                }
                user.Password = request.NewPassword;
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Profile updated successfully!" });
        }
    }

    public class RegisterDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class UpdateProfileDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
    }
}