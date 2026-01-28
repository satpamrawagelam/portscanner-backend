using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using Microsoft.Data.SqlClient;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class PortController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PortController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetByGroup(int groupId)
        {
            var pGroup = new SqlParameter("@GroupId", groupId);
            var ports = await _context.PortMasters
                .FromSqlRaw("EXEC sp_GetPortsByGroup @GroupId", pGroup)
                .ToListAsync();

            return Ok(ports);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PortMasterDto req)
        {
            var pGroup = new SqlParameter("@GroupId", req.Pm_portGroup);
            var pPort = new SqlParameter("@PortNumber", req.Pm_portNumber);
            var pDesc = new SqlParameter("@Desc", req.Pm_desc ?? ""); 
            var pSev = new SqlParameter("@Severity", req.Pm_severity ?? "Low");

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_AddPortMaster @GroupId, @PortNumber, @Desc, @Severity", 
                pGroup, pPort, pDesc, pSev
            );

            return Ok(new { message = "Success" });
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] PortMasterDto req)
        {
            var pId = new SqlParameter("@Id", id);
            var pPort = new SqlParameter("@PortNumber", req.Pm_portNumber);
            var pDesc = new SqlParameter("@Desc", req.Pm_desc ?? "");
            var pSev = new SqlParameter("@Severity", req.Pm_severity ?? "Low");

            await _context.Database.ExecuteSqlRawAsync(
                "EXEC sp_UpdatePortMaster @Id, @PortNumber, @Desc, @Severity", 
                pId, pPort, pDesc, pSev
            );

            return Ok(new { message = "Updated" });
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var pId = new SqlParameter("@Id", id);
            await _context.Database.ExecuteSqlRawAsync("EXEC sp_DeletePortMaster @Id", pId);
            return Ok(new { message = "Deleted" });
        }
    }
}