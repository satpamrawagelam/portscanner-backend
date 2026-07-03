using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;
using Microsoft.Data.SqlClient;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            var pUser = new SqlParameter("@Username", req.Username);
            var pPass = new SqlParameter("@Password", req.Password);

            var user = await _context.Users
                .FromSqlRaw("EXEC V2_sp_LoginUser @Username, @Password", pUser, pPass)
                .AsNoTracking()
                .ToListAsync();

            var loggedInUser = user.FirstOrDefault();
            if (loggedInUser == null)
            {
                return Unauthorized(new { message = "Invalid username or password." });
            }

            return Ok(new { 
                message = "Login successful", 
                userId = loggedInUser.Id,
                username = loggedInUser.Username
            });
        }

        [HttpPost]
        public async Task<IActionResult> Register([FromBody] LoginRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            var pUser = new SqlParameter("@Username", req.Username);
            var pPass = new SqlParameter("@Password", req.Password);

            try
            {
                await _context.Database.ExecuteSqlRawAsync("EXEC V2_sp_RegisterUser @Username, @Password", pUser, pPass);
                return Ok(new { message = "User registered successfully." });
            }
            catch (Exception ex)
            {
                var sqlEx = ex.InnerException as SqlException ?? ex as SqlException;
                if (sqlEx != null)
                {
                    return BadRequest(new { message = sqlEx.Message });
                }
                return StatusCode(500, new { message = "Internal server error", detail = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .FromSqlRaw("EXEC V2_sp_GetAllUsers")
                .AsNoTracking()
                .ToListAsync();
            return Ok(users);
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> UpdateUser(int id, [FromBody] LoginRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            {
                return BadRequest(new { message = "Username and password are required." });
            }

            var pId = new SqlParameter("@Id", id);
            var pUser = new SqlParameter("@Username", req.Username);
            var pPass = new SqlParameter("@Password", req.Password);

            try
            {
                await _context.Database.ExecuteSqlRawAsync("EXEC V2_sp_UpdateUser @Id, @Username, @Password", pId, pUser, pPass);
                return Ok(new { message = "User updated successfully." });
            }
            catch (Exception ex)
            {
                var sqlEx = ex.InnerException as SqlException ?? ex as SqlException;
                if (sqlEx != null)
                {
                    return BadRequest(new { message = sqlEx.Message });
                }
                return StatusCode(500, new { message = "Internal server error", detail = ex.Message });
            }
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var pId = new SqlParameter("@Id", id);
            try
            {
                await _context.Database.ExecuteSqlRawAsync("EXEC V2_sp_DeleteUser @Id", pId);
                return Ok(new { message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Internal server error", detail = ex.Message });
            }
        }
    }
}
