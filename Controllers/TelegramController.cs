using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models.Dto;
using Microsoft.Data.SqlClient;
using System.Net;


namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class TelegramController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TelegramController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("{ip}")]
        public async Task<IActionResult> CheckPortByHost(string ip){
            if(string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out var _)){
                return Ok(new {text = "IP Address is invalid"});
            }

            var IpAddress = new SqlParameter("@IpAddress", ip);

            var data = await _context.CheckPortByHostDtos
                .FromSqlRaw("EXEC V2_sp_CheckPortByHost @IpAddress", IpAddress)
                .ToListAsync();

            if (data == null || !data.Any()) return Ok("IP not found or no scan data");    

            return Ok(data);
        }

        [HttpGet("{branch}")]
        public async Task<IActionResult> CheckPortByBranch(string branch){
            if(string.IsNullOrWhiteSpace(branch)){
                return Ok(new {text = "Branch is invalid"});
            }

            var Branch = new SqlParameter("@BranchName", branch);

            var data = await _context.CheckPortByBranchRawDtos
                .FromSqlRaw("EXEC V2_sp_CheckPortByBranch @BranchName", Branch)
                .ToListAsync();

            if (data == null || !data.Any()) return Ok("Branch not found or no scan data");

            var headerData = data.First();

            var finalResult = new CheckPortByBranchDto
            {
                BranchName = headerData.BranchName,
                BranchCidr = headerData.BranchCidr,
                
                Results = data.Select(row => new CheckPortByHostDto
                {
                    IpAddress = row.IpAddress,
                    HostStatus = row.HostStatus,
                    Port_number = string.IsNullOrWhiteSpace(row.Port_number) ? "-" : row.Port_number,
                    LastScanned = row.LastScanned
                }).ToList()
            };
            
            return Ok(finalResult);
        }

        [HttpGet("")] 
        public async Task<IActionResult> CheckSpesificPortAndHost([FromQuery] SpesificPortRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Ip) || string.IsNullOrWhiteSpace(req.Ports))
            {
                return Ok(new { text = "⚠️ *Format perintah tidak lengkap!*\nContoh penggunaan: `/status 10.10.10.10 80,443`" });
            }

            if (!IPAddress.TryParse(req.Ip, out _))
            {
                return Ok(new { text = "⚠️ *Format IP Address tidak valid!*\nContoh: `/status 10.10.10.10 80,443`" });
            }
            
            try
            {
                var data = await _context.Set<SpesificPortStatusDto>()
                    .FromSqlInterpolated($"EXEC V2_sp_CheckSpesificPortAndHost @IpAddress = {req.Ip}, @Ports = {req.Ports}")
                    .ToListAsync();

                if (data == null || !data.Any())
                {
                    return Ok(new { text = $"⚠️ *IP {req.Ip} tidak ditemukan terdaftar di database!*" });
                }

                return Ok(new { results = data });
            }
            catch (Exception ex)
            {
                return Ok(new { text = "⚠️ *Terjadi kesalahan pada server saat menarik data.*" });
            }
        }

        [HttpGet("")]
        public async Task<IActionResult> CheckSpesificPortBySegment([FromQuery] SegmentPortRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Cidr) || string.IsNullOrWhiteSpace(req.Ports))
            {
                return Ok(new { text = "⚠️ *Format perintah tidak lengkap!*\nContoh: `/check 10.10.10.0/28 20,443`" });
            }

            try
            {
                var data = await _context.Set<SegmentPortRawDto>()
                    .FromSqlInterpolated($"EXEC V2_sp_CheckSpesificPortBySegment @Cidr = {req.Cidr}, @Ports = {req.Ports}")
                    .ToListAsync();

                if (data == null || !data.Any())
                {
                    return Ok(new { text = $"⚠️ *Segmen IP {req.Cidr} tidak ditemukan di database!*" });
                }

                var finalResult = new
                {
                    cidr = req.Cidr,
                    results = data.Select(row => new 
                    {
                        ipAddress = row.IpAddress,
                        hostStatus = row.HostStatus,
                        openPorts = string.IsNullOrWhiteSpace(row.OpenPorts) ? "-" : row.OpenPorts,
                        lastScanned = row.LastScanned
                    }).ToList()
                };

                return Ok(finalResult);
            }
            catch (Exception ex)
            {
                return Ok(new { text = "⚠️ *Terjadi kesalahan pada server saat menarik data.*" });
            }
        }

        [HttpGet("")]
        public async Task<IActionResult> CheckHostsBySpesificPorts([FromQuery] CheckPortsRequestDto req)
        {
            if (string.IsNullOrWhiteSpace(req.Ports))
            {
                return Ok(new { text = "⚠️ *Format perintah tidak lengkap!*\nContoh: `/CP 80,443`" });
            }

            try
            {
                var data = await _context.Set<SegmentPortRawDto>()
                    .FromSqlInterpolated($"EXEC V2_sp_CheckHostsBySpesificPorts @Ports = {req.Ports}")
                    .ToListAsync();

                if (data == null || !data.Any())
                {
                    return Ok(new { text = $"✅ *Aman!* Tidak ditemukan host yang memiliki port {req.Ports} dalam keadaan terbuka." });
                }

                var finalResult = new
                {
                    portsRequested = req.Ports,
                    results = data.Select(row => new 
                    {
                        ipAddress = row.IpAddress,
                        hostStatus = row.HostStatus,
                        openPorts = row.OpenPorts,
                        lastScanned = row.LastScanned
                    }).ToList()
                };

                return Ok(finalResult);
            }
            catch (Exception ex)
            {
                return Ok(new { text = "⚠️ *Terjadi kesalahan pada server saat menarik data.*" });
            }
        }
    }
}