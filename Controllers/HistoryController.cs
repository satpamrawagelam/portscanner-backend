using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using portscanner_backend.Data;
using portscanner_backend.Models.Dto;

namespace portscanner_backend.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class HistoryController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public HistoryController(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [HttpGet]
        public async Task<IActionResult> GetHistory(
            [FromQuery] string scanType = "manual", 
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = "",
            [FromQuery] DateTime? dateStart = null,
            [FromQuery] DateTime? dateEnd = null)
        {
            try
            {
                string cacheKey = $"History_{scanType}_{page}_{pageSize}_{search}_{dateStart:yyyyMMdd}_{dateEnd:yyyyMMdd}";

                var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                    var pScanType   = new Microsoft.Data.SqlClient.SqlParameter("@ScanType",   scanType);
                    var pSearch     = new Microsoft.Data.SqlClient.SqlParameter("@SearchTerm", search ?? "");
                    var pPage       = new Microsoft.Data.SqlClient.SqlParameter("@PageNumber",  page);
                    var pSize       = new Microsoft.Data.SqlClient.SqlParameter("@PageSize",    pageSize);
                    var pDateStart  = new Microsoft.Data.SqlClient.SqlParameter("@DateStart",   (object?)dateStart ?? DBNull.Value);
                    var pDateEnd    = new Microsoft.Data.SqlClient.SqlParameter("@DateEnd",     (object?)dateEnd   ?? DBNull.Value);

                    var rawData = await _context.Set<ScanHistoryDto>()
                        .FromSqlRaw("EXEC V2_sp_GetScanHistoryLog @ScanType, @SearchTerm, @PageNumber, @PageSize, @DateStart, @DateEnd", 
                            pScanType, pSearch, pPage, pSize, pDateStart, pDateEnd)
                        .ToListAsync();

                    return new 
                    {
                        CurrentPage = page,
                        PageSize = pageSize,
                        Data = rawData
                    };
                });

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
        
        [HttpGet]
        public async Task<IActionResult> GetReport(
            [FromQuery] DateTime? dateStart = null,
            [FromQuery] DateTime? dateEnd   = null,
            [FromQuery] string scanType     = "all",
            [FromQuery] string? search      = "")
        {
            try
            {
                // Default ke bulan berjalan jika tidak ada filter
                var now   = DateTime.Now;
                var start = dateStart ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0);
                var end   = dateEnd   ?? new DateTime(now.Year, now.Month,
                                DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59);

                var conn = _context.Database.GetDbConnection();
                await conn.OpenAsync();

                try
                {
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "EXEC V2_sp_GetScanHistoryReport @DateStart, @DateEnd, @ScanType, @SearchTerm";

                    var pStart = cmd.CreateParameter();
                    pStart.ParameterName = "@DateStart";
                    pStart.Value = start;
                    cmd.Parameters.Add(pStart);

                    var pEnd = cmd.CreateParameter();
                    pEnd.ParameterName = "@DateEnd";
                    pEnd.Value = end;
                    cmd.Parameters.Add(pEnd);

                    var pScanType = cmd.CreateParameter();
                    pScanType.ParameterName = "@ScanType";
                    pScanType.Value = scanType;
                    cmd.Parameters.Add(pScanType);

                    var pSearch = cmd.CreateParameter();
                    pSearch.ParameterName = "@SearchTerm";
                    pSearch.Value = search ?? "";
                    cmd.Parameters.Add(pSearch);

                    using var reader = await cmd.ExecuteReaderAsync();

                    // ── RS 1: Summary ─────────────────────────────────────────
                    var summary = new ScanReportSummaryDto();
                    if (await reader.ReadAsync())
                    {
                        summary.TotalScan               = reader.GetInt32(reader.GetOrdinal("TotalScan"));
                        summary.TotalOpenPortFindings   = reader.GetInt32(reader.GetOrdinal("TotalOpenPortFindings"));
                        summary.TotalHostWithOpenPort   = reader.GetInt32(reader.GetOrdinal("TotalHostWithOpenPort"));
                        summary.HighSeverityPortCount   = reader.GetInt32(reader.GetOrdinal("HighSeverityPortCount"));
                        summary.MediumSeverityPortCount = reader.GetInt32(reader.GetOrdinal("MediumSeverityPortCount"));
                    }

                    // ── RS 2: Top Branches ────────────────────────────────────
                    await reader.NextResultAsync();
                    var topBranches = new List<TopBranchReportDto>();
                    while (await reader.ReadAsync())
                        topBranches.Add(new TopBranchReportDto
                        {
                            BranchName    = reader.GetString(reader.GetOrdinal("BranchName")),
                            OpenPortCount = reader.GetInt32(reader.GetOrdinal("OpenPortCount")),
                            RiskScore     = reader.GetInt32(reader.GetOrdinal("RiskScore"))
                        });

                    // ── RS 3: Top Hosts ───────────────────────────────────────
                    await reader.NextResultAsync();
                    var topHosts = new List<TopHostReportDto>();
                    while (await reader.ReadAsync())
                        topHosts.Add(new TopHostReportDto
                        {
                            IpAddress     = reader.GetString(reader.GetOrdinal("IpAddress")),
                            BranchName    = reader.GetString(reader.GetOrdinal("BranchName")),
                            OpenPortCount = reader.GetInt32(reader.GetOrdinal("OpenPortCount")),
                            RiskScore     = reader.GetInt32(reader.GetOrdinal("RiskScore"))
                        });

                    // ── RS 4: Top Ports ───────────────────────────────────────
                    await reader.NextResultAsync();
                    var topPorts = new List<TopPortReportDto>();
                    while (await reader.ReadAsync())
                        topPorts.Add(new TopPortReportDto
                        {
                            PortNumber = reader.GetInt32(reader.GetOrdinal("PortNumber")),
                            Severity   = reader.GetString(reader.GetOrdinal("Severity")),
                            PortDesc   = reader.GetString(reader.GetOrdinal("PortDesc")),
                            OpenCount  = reader.GetInt32(reader.GetOrdinal("OpenCount"))
                        });

                    // ── RS 5: Detail History ──────────────────────────────────
                    await reader.NextResultAsync();
                    var details = new List<ScanHistoryDto>();
                    while (await reader.ReadAsync())
                        details.Add(new ScanHistoryDto
                        {
                            ScanDate   = reader.GetDateTime(reader.GetOrdinal("ScanDate")),
                            ScanTitle  = reader.GetString(reader.GetOrdinal("ScanTitle")),
                            ScanType   = reader.GetString(reader.GetOrdinal("ScanType")),
                            BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                            IpAddress  = reader.GetString(reader.GetOrdinal("IpAddress")),
                            HostStatus = reader.GetBoolean(reader.GetOrdinal("HostStatus")),
                            OpenPorts  = reader.IsDBNull(reader.GetOrdinal("OpenPorts"))
                                          ? "-" : reader.GetString(reader.GetOrdinal("OpenPorts"))
                        });

                    return Ok(new ScanReportResponseDto
                    {
                        PeriodeStart    = start.ToString("yyyy-MM-dd"),
                        PeriodeEnd      = end.ToString("yyyy-MM-dd"),
                        Summary         = summary,
                        TopBranches     = topBranches,
                        TopHosts        = topHosts,
                        TopPorts        = topPorts,
                        DetailHistory   = details
                    });
                }
                finally
                {
                    await conn.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}
