using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class PortGroupController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PortGroupController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var groups = await _context.PortGroups
                .FromSqlRaw("EXEC V2_sp_GetAllPortGroupsDD")
                .ToListAsync();

            return Ok(groups);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllPortGroup()
        {
            var groups = await _context.Set<PortGroupResult>()
                .FromSqlRaw("EXEC V2_sp_GetAllPortGroups")
                .ToListAsync();
            return Ok(groups);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] PortGroupDto req)
        {
            var param = new SqlParameter("@Name", req.Pg_name);
            await _context.Database.ExecuteSqlRawAsync("EXEC V2_sp_AddPortGroup @Name", param);
            return Ok(new { message = "Success" });
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PortGroupDto req)
        {
            var pId = new SqlParameter("@Id", id);
            var pName = new SqlParameter("@Name", req.Pg_name);
            await _context.Database.ExecuteSqlRawAsync("EXEC V2_sp_UpdatePortGroup @Id, @Name", pId, pName);
            return Ok(new { message = "Updated" });
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            try 
            {
                var pId = new SqlParameter("@Id", id);
                await _context.Database.ExecuteSqlRawAsync("EXEC V2_sp_DeletePortGroup @Id", pId);
                return Ok(new { message = "Deleted" });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message); 
            }
        }
    }
}
