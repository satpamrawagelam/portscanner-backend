using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class ScheduleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ScheduleController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var schedules = await _context.ScanSchedules
                .OrderByDescending(x => x.Sch_id)
                .ToListAsync();
                
                
                Console.WriteLine(schedules);

            return Ok(schedules);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ScanSchedule req)
        {
            try
            {
                // 1. Hitung Kapan Next Run Pertamanya (WIB)
                DateTime now = DateTime.UtcNow.AddHours(7); 
                DateTime todayRun = now.Date.Add(req.Sch_time);

                if (todayRun > now)
                    req.Sch_nextRun = todayRun;
                else
                    req.Sch_nextRun = todayRun.AddDays(1);

                req.Sch_createdDate = now;
                req.Sch_lastRun = null;
                req.Sch_isActive = true;

                _context.ScanSchedules.Add(req);
                await _context.SaveChangesAsync();

                if (req.TargetBranchIds != null && req.TargetBranchIds.Any())
                {
                    var targets = req.TargetBranchIds.Select(branchId => new ScanScheduleTarget
                    {
                        Tgt_schId = req.Sch_id,
                        Tgt_branchId = branchId
                    });
                    
                    await _context.ScanScheduleTargets.AddRangeAsync(targets);
                    await _context.SaveChangesAsync();
                }

                return Ok(new { message = "Jadwal berhasil disimpan", nextRun = req.Sch_nextRun });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.ScanSchedules.FindAsync(id);
            if (item == null) return NotFound();

            _context.ScanSchedules.Remove(item);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Jadwal dihapus" });
        }
        
        [HttpPost("{id}")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var item = await _context.ScanSchedules.FindAsync(id);
            if (item == null) return NotFound();

            item.Sch_isActive = !item.Sch_isActive;
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "Status berhasil diubah", isActive = item.Sch_isActive });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var schedule = await _context.ScanSchedules.FindAsync(id);
            if (schedule == null) return NotFound("Jadwal tidak ditemukan");

            var targetBranches = await (from t in _context.ScanScheduleTargets
                                        join b in _context.Branches on t.Tgt_branchId equals b.Branch_id
                                        where t.Tgt_schId == id
                                        select new TargetBranchDto
                                        {
                                            BranchId = b.Branch_id,
                                            BranchName = b.Branch_name,
                                            BranchCidr = b.Branch_cidr
                                        }).ToListAsync();

            var dto = new ScheduleDetailDto
            {
                Sch_id = schedule.Sch_id,
                Sch_title = schedule.Sch_title,
                Sch_frequency = schedule.Sch_frequency,
                Sch_time = schedule.Sch_time.ToString(@"hh\:mm\:ss"),
                Sch_portMode = schedule.Sch_portMode,
                Sch_targetPortGroupId = schedule.Sch_targetPortGroupId,
                Sch_targetManualPort = schedule.Sch_targetManualPort,
                Sch_nextRun = schedule.Sch_nextRun,
                Sch_isActive = schedule.Sch_isActive,
                Targets = targetBranches
            };

            return Ok(dto);
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ScanSchedule req)
        {
            var schedule = await _context.ScanSchedules.FindAsync(id);
            if (schedule == null) return NotFound("Jadwal tidak ditemukan");

            schedule.Sch_title = req.Sch_title;
            schedule.Sch_frequency = req.Sch_frequency;
            schedule.Sch_time = req.Sch_time;
            schedule.Sch_portMode = req.Sch_portMode;
            schedule.Sch_targetPortGroupId = req.Sch_targetPortGroupId;
            schedule.Sch_targetManualPort = req.Sch_targetManualPort;

            DateTime now = DateTime.UtcNow.AddHours(7);
            DateTime newRunTime = now.Date.Add(req.Sch_time);
            if (newRunTime > now) schedule.Sch_nextRun = newRunTime;
            else schedule.Sch_nextRun = newRunTime.AddDays(1);

            var oldTargets = _context.ScanScheduleTargets.Where(t => t.Tgt_schId == id);
            _context.ScanScheduleTargets.RemoveRange(oldTargets);

            if (req.TargetBranchIds != null && req.TargetBranchIds.Any())
            {
                var newTargets = req.TargetBranchIds.Select(bid => new ScanScheduleTarget
                {
                    Tgt_schId = id,
                    Tgt_branchId = bid
                });
                await _context.ScanScheduleTargets.AddRangeAsync(newTargets);
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Jadwal berhasil diupdate" });
        }

    }
}