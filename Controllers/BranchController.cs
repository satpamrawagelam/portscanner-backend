using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models.Dto;
using Microsoft.Data.SqlClient;


namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class BranchController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BranchController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var branches = await _context.Branches
                .FromSqlRaw("EXEC sp_GetAllBranches")
                .ToListAsync();

            return Ok(branches);
        }

        

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var data = await _context.Branches
                .FromSqlRaw("EXEC sp_GetAllBranches")
                .ToListAsync();
            return Ok(data);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BranchDto req)
        {
            var pName = new SqlParameter("@Name", req.Branch_name);
            var pCidr = new SqlParameter("@Cidr", req.Branch_cidr ?? "");

            await _context.Database.ExecuteSqlRawAsync("EXEC sp_AddBranch @Name, @Cidr", pName, pCidr);
            return Ok(new { message = "Branch Created" });
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] BranchDto req)
        {
            var pId = new SqlParameter("@Id", id);
            var pName = new SqlParameter("@Name", req.Branch_name);
            var pCidr = new SqlParameter("@Cidr", req.Branch_cidr ?? "");

            await _context.Database.ExecuteSqlRawAsync("EXEC sp_UpdateBranch @Id, @Name, @Cidr", pId, pName, pCidr);
            return Ok(new { message = "Branch Updated" });
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var pId = new SqlParameter("@Id", id);
            await _context.Database.ExecuteSqlRawAsync("EXEC sp_DeleteBranch @Id", pId);
            return Ok(new { message = "Branch Deleted" });
        }
    }
}