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
                
            return Ok(schedules);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ScheduleRequestDto req)
        {
            try
            {
                DateTime now = DateTime.UtcNow.AddHours(7); 
                
                TimeSpan parsedTime;
                if (!TimeSpan.TryParse(req.Sch_time, out parsedTime))
                {
                    return BadRequest(new { message = "Format waktu tidak valid. Gunakan HH:mm:ss" });
                }

                DateTime todayRun = now.Date.Add(parsedTime);
                DateTime nextRun = todayRun;
                
                if (todayRun <= now)
                {
                    if (req.Sch_frequency == "5 Minutes")
                        nextRun = now.AddMinutes(5);
                    else if (req.Sch_frequency == "Hourly")
                        nextRun = now.AddHours(1);
                    else
                        nextRun = todayRun.AddDays(1);
                }

                var newSchedule = new ScanSchedule
                {
                    Sch_title = req.Sch_title,
                    Sch_frequency = req.Sch_frequency,
                    Sch_time = parsedTime,
                    Sch_portMode = req.Sch_pgId == 0 ? "all" : req.Sch_portMode,
                    Sch_pgId = req.Sch_pgId == 0 ? null : (req.Sch_portMode == "all" ? null : req.Sch_pgId),
                    Sch_createdDate = now,
                    Sch_isActive = true,
                    Sch_nextRun = nextRun
                };

                _context.ScanSchedules.Add(newSchedule);
                await _context.SaveChangesAsync();

                if (req.TargetBranchIds != null && req.TargetBranchIds.Any())
                {
                    var targets = req.TargetBranchIds.Select(branchId => new ScanScheduleTarget
                    {
                        Tgt_schId = newSchedule.Sch_id,
                        Tgt_branchId = branchId
                    });
                    await _context.ScanScheduleTargets.AddRangeAsync(targets);
                }

                if ((req.Sch_portMode == "Custom" || req.Sch_portMode == "single") && req.Sch_targetManualPort != null && req.Sch_targetManualPort.Any())
                {
                    // 1. Simpan string list-nya langsung di tabel ScanSchedule agar port custom tak terdaftar tidak hilang
                    newSchedule.Sch_customPorts = string.Join(",", req.Sch_targetManualPort);
                    
                    // 2. Jika secara kebetulan ada Port tsb di PortMasters, buat relasinya jg untuk UI
                    var pmIds = await _context.PortMasters
                        .Where(pm => req.Sch_targetManualPort.Contains(pm.Pm_port_number))
                        .Select(pm => pm.Pm_id)
                        .ToListAsync();

                    var schPorts = pmIds.Select(pmId => new ScanSchedulePort
                    {
                        Sch_id = newSchedule.Sch_id,
                        Pm_id = pmId
                    });
                    await _context.ScanSchedulePorts.AddRangeAsync(schPorts);
                }

                await _context.SaveChangesAsync();

                return Ok(new { message = "Jadwal berhasil disimpan", nextRun = newSchedule.Sch_nextRun });
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

            var oldTargets = _context.ScanScheduleTargets.Where(t => t.Tgt_schId == id);
            _context.ScanScheduleTargets.RemoveRange(oldTargets);

            var oldPorts = _context.ScanSchedulePorts.Where(p => p.Sch_id == id);
            _context.ScanSchedulePorts.RemoveRange(oldPorts);

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

            List<int>? parsedManualPorts = null;
            if (schedule.Sch_portMode == "Custom" || schedule.Sch_portMode == "single")
            {
                if (!string.IsNullOrEmpty(schedule.Sch_customPorts))
                {
                    parsedManualPorts = schedule.Sch_customPorts.Split(',')
                                        .Select(int.Parse)
                                        .ToList();
                }
                else
                {
                    parsedManualPorts = await _context.ScanSchedulePorts
                        .Include(sp => sp.PortMaster)
                        .Where(sp => sp.Sch_id == schedule.Sch_id)
                        .Select(sp => sp.PortMaster!.Pm_port_number)
                        .ToListAsync();
                }
            }

            var dto = new ScheduleDetailDto
            {
                Sch_id = schedule.Sch_id,
                Sch_title = schedule.Sch_title,
                Sch_frequency = schedule.Sch_frequency,
                Sch_time = schedule.Sch_time.ToString(@"hh\:mm\:ss"),
                Sch_portMode = schedule.Sch_portMode == "all" ? "group" : schedule.Sch_portMode,
                Sch_pgId = schedule.Sch_portMode == "all" ? 0 : schedule.Sch_pgId,
                Sch_targetManualPort = parsedManualPorts,
                Sch_nextRun = schedule.Sch_nextRun,
                Sch_isActive = schedule.Sch_isActive,
                Targets = targetBranches
            };

            return Ok(dto);
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] ScheduleRequestDto req)
        {
            var schedule = await _context.ScanSchedules.FindAsync(id);
            if (schedule == null) return NotFound("Jadwal tidak ditemukan");

            TimeSpan parsedTime;
            if (!TimeSpan.TryParse(req.Sch_time, out parsedTime))
            {
                return BadRequest(new { message = "Format waktu tidak valid. Gunakan HH:mm:ss" });
            }

            schedule.Sch_title = req.Sch_title;
            schedule.Sch_frequency = req.Sch_frequency;
            schedule.Sch_time = parsedTime;
            schedule.Sch_portMode = req.Sch_pgId == 0 ? "all" : req.Sch_portMode;
            schedule.Sch_pgId = req.Sch_pgId == 0 ? null : (req.Sch_portMode == "all" ? null : req.Sch_pgId);

            DateTime now = DateTime.UtcNow.AddHours(7);
            DateTime newRunTime = now.Date.Add(parsedTime);
            DateTime nextRun = newRunTime;
            
            if (newRunTime <= now)
            {
                if (req.Sch_frequency == "5 Minutes")
                    nextRun = now.AddMinutes(5);
                else if (req.Sch_frequency == "Hourly")
                    nextRun = now.AddHours(1);
                else if (req.Sch_frequency == "Daily")
                    nextRun = newRunTime.AddDays(1);
                else if (req.Sch_frequency == "Weekly")
                    nextRun = newRunTime.AddDays(7);
            }

            schedule.Sch_nextRun = nextRun;

            var oldTargets = _context.ScanScheduleTargets.Where(t => t.Tgt_schId == id);
            _context.ScanScheduleTargets.RemoveRange(oldTargets);

            var oldPorts = _context.ScanSchedulePorts.Where(p => p.Sch_id == id);
            _context.ScanSchedulePorts.RemoveRange(oldPorts);

            if (req.TargetBranchIds != null && req.TargetBranchIds.Any())
            {
                var newTargets = req.TargetBranchIds.Select(bid => new ScanScheduleTarget
                {
                    Tgt_schId = id,
                    Tgt_branchId = bid
                });
                await _context.ScanScheduleTargets.AddRangeAsync(newTargets);
            }

            if ((req.Sch_portMode == "Custom" || req.Sch_portMode == "single") && req.Sch_targetManualPort != null && req.Sch_targetManualPort.Any())
            {
                schedule.Sch_customPorts = string.Join(",", req.Sch_targetManualPort);

                var pmIds = await _context.PortMasters
                    .Where(pm => req.Sch_targetManualPort.Contains(pm.Pm_port_number))
                    .Select(pm => pm.Pm_id)
                    .ToListAsync();

                var schPorts = pmIds.Select(pmId => new ScanSchedulePort
                {
                    Sch_id = id,
                    Pm_id = pmId
                });
                await _context.ScanSchedulePorts.AddRangeAsync(schPorts);
            }
            else
            {
                schedule.Sch_customPorts = null;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "Jadwal berhasil diupdate" });
        }
    }
}