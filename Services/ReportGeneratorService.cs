using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Extensions.Configuration;
using MiniExcelLibs;

namespace portscanner_backend.Services
{
    public class ReportGeneratorService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWebHostEnvironment _env;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

        public ReportGeneratorService(IServiceScopeFactory scopeFactory, IWebHostEnvironment env, Microsoft.Extensions.Configuration.IConfiguration config)
        {
            _scopeFactory = scopeFactory;
            _env = env;
            _config = config;
            // Configure QuestPDF license (Community is free for small companies)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task GenerateReportAsync(DateTime dateStart, DateTime dateEnd, string reportTitle, string reportType, int? existingReportId = null, string scanType = "all", string search = "")
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Fetch Data
            var data = await FetchReportDataAsync(dbContext, dateStart, dateEnd, scanType, search);

            // 2. Setup File Path
            var outputPath = _config["ReportSettings:OutputPath"] ?? "../Reports";
            string reportsFolder = Path.IsPathRooted(outputPath)
                ? outputPath
                : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));

            if (!Directory.Exists(reportsFolder))
                Directory.CreateDirectory(reportsFolder);

            string fileName = $"{reportType}_Report_{DateTime.Now:yyyyMMddHHmmss}.pdf";
            string filePath = Path.Combine(reportsFolder, fileName);
            string urlPath = $"/reports/{fileName}";

            // 3. Generate PDF
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Element(c => ComposeHeader(c, dateStart, dateEnd));
                    page.Content().Element(c => ComposeContent(c, data));
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf(filePath);

            // Generate companion Excel file containing details using memory-efficient database streaming
            string excelFileName = Path.ChangeExtension(fileName, ".xlsx");
            string excelFilePath = Path.Combine(reportsFolder, excelFileName);
            await StreamExcelReportAsync(dbContext, dateStart, dateEnd, scanType, search, excelFilePath);

            // 4. Save/Update DB
            if (existingReportId.HasValue)
            {
                var record = await dbContext.GeneratedReports.FindAsync(existingReportId.Value);
                if (record != null)
                {
                    record.FilePath = urlPath;
                    record.CreatedAt = DateTime.Now; // Update created time to completion time
                    await dbContext.SaveChangesAsync();
                }
            }
            else
            {
                var reportRecord = new GeneratedReport
                {
                    Title = reportTitle,
                    Type = reportType,
                    DateStart = dateStart,
                    DateEnd = dateEnd,
                    FilePath = urlPath,
                    CreatedAt = DateTime.Now
                };

                dbContext.GeneratedReports.Add(reportRecord);
                await dbContext.SaveChangesAsync();
            }
        }

        private async Task<ScanReportResponseDto> FetchReportDataAsync(AppDbContext dbContext, DateTime dateStart, DateTime dateEnd, string scanType, string search)
        {
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "V2_sp_GetScanHistoryReport";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandTimeout = 7200;

            var pStart = cmd.CreateParameter();
            pStart.ParameterName = "@DateStart";
            pStart.Value = dateStart;
            cmd.Parameters.Add(pStart);

            var pEnd = cmd.CreateParameter();
            pEnd.ParameterName = "@DateEnd";
            pEnd.Value = dateEnd;
            cmd.Parameters.Add(pEnd);

            var pScanType = cmd.CreateParameter();
            pScanType.ParameterName = "@ScanType";
            pScanType.Value = scanType;
            cmd.Parameters.Add(pScanType);

            var pSearch = cmd.CreateParameter();
            pSearch.ParameterName = "@SearchTerm";
            pSearch.Value = search ?? "";
            cmd.Parameters.Add(pSearch);

            await dbContext.Database.OpenConnectionAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            var summary = new ScanReportSummaryDto();
            if (await reader.ReadAsync())
            {
                summary.TotalScan = reader.GetInt32(reader.GetOrdinal("TotalScan"));
                summary.TotalOpenPortFindings = reader.GetInt32(reader.GetOrdinal("TotalOpenPortFindings"));
                summary.TotalHostWithOpenPort = reader.GetInt32(reader.GetOrdinal("TotalHostWithOpenPort"));
                summary.HighSeverityPortCount = reader.GetInt32(reader.GetOrdinal("HighSeverityPortCount"));
                summary.MediumSeverityPortCount = reader.GetInt32(reader.GetOrdinal("MediumSeverityPortCount"));
            }

            await reader.NextResultAsync();
            var topBranches = new List<TopBranchReportDto>();
            while (await reader.ReadAsync())
                topBranches.Add(new TopBranchReportDto
                {
                    BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                    OpenPortCount = reader.GetInt32(reader.GetOrdinal("OpenPortCount")),
                    RiskScore = reader.GetInt32(reader.GetOrdinal("RiskScore"))
                });

            await reader.NextResultAsync();
            var topHighSeverityBranches = new List<TopBranchReportDto>();
            while (await reader.ReadAsync())
                topHighSeverityBranches.Add(new TopBranchReportDto
                {
                    BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                    OpenPortCount = reader.GetInt32(reader.GetOrdinal("HighPortCount")),
                    RiskScore = reader.GetInt32(reader.GetOrdinal("RiskScore"))
                });

            await reader.NextResultAsync();
            var topHosts = new List<TopHostReportDto>();
            while (await reader.ReadAsync())
                topHosts.Add(new TopHostReportDto
                {
                    IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                    BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                    OpenPortCount = reader.GetInt32(reader.GetOrdinal("OpenPortCount")),
                    RiskScore = reader.GetInt32(reader.GetOrdinal("RiskScore"))
                });

            await reader.NextResultAsync();
            var topPorts = new List<TopPortReportDto>();
            while (await reader.ReadAsync())
                topPorts.Add(new TopPortReportDto
                {
                    PortNumber = reader.GetInt32(reader.GetOrdinal("PortNumber")),
                    Severity = reader.GetString(reader.GetOrdinal("Severity")),
                    PortDesc = reader.GetString(reader.GetOrdinal("PortDesc")),
                    OpenCount = reader.GetInt32(reader.GetOrdinal("OpenCount"))
                });

            // Hapus pembacaan detail history (result set ke-6) ke RAM C# untuk menghindari OutOfMemory.
            // Data detail lengkap akan langsung ditulis ke Excel dengan metode streaming.

            return new ScanReportResponseDto
            {
                Summary = summary,
                TopBranches = topBranches,
                TopHosts = topHosts,
                TopPorts = topPorts,
                TopHighSeverityBranches = topHighSeverityBranches,
                DetailHistory = new List<ScanHistoryDto>() // Diisi list kosong untuk menghemat memory
            };
        }

        private void ComposeHeader(IContainer container, DateTime start, DateTime end)
        {
            container.Background("#0f172a").Padding(10).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("EXTERNAL EXPOSURE CHECKER REPORT").FontSize(14).FontColor(Colors.White).Bold();
                    col.Item().Text($"Periode: {start:dd MMM yyyy} s/d {end:dd MMM yyyy}").FontSize(8).FontColor(Colors.White);
                });
                row.ConstantItem(150).AlignRight().Text($"Generated: {DateTime.Now:dd MMM yyyy HH:mm}").FontSize(8).FontColor(Colors.White);
            });
        }

        private void ComposeContent(IContainer container, ScanReportResponseDto data)
        {
            container.PaddingVertical(10).Column(col =>
            {
                // PAGE 1: Executive Summary, Top Branches (Risk) & Top Branches (High Severity)
                col.Item().Row(row =>
                {
                    DrawSummaryCard(row, "Total Scan", data.Summary.TotalScan.ToString(), "#0f172a");
                    DrawSummaryCard(row, "Total Open Port Findings", data.Summary.TotalOpenPortFindings.ToString(), "#dc3545");
                    DrawSummaryCard(row, "Host with Open Port", data.Summary.TotalHostWithOpenPort.ToString(), "#fd7e14");
                    DrawSummaryCard(row, "HIGH Severity Port Exposed", data.Summary.HighSeverityPortCount.ToString(), "#b4320a");
                    DrawSummaryCard(row, "MEDIUM Severity Port Exposed", data.Summary.MediumSeverityPortCount.ToString(), "#856404");
                });

                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(2).Text("Top 10 Branches with High Risk Exposure").Bold().FontSize(10);
                        DrawTopBranchesTable(c, data.TopBranches);
                    });
                    row.ConstantItem(10); // spacing
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(2).Text("Top 10 Branches with strict HIGH Severity Port Exposure").Bold().FontSize(10);
                        DrawTopBranchesTable(c, data.TopHighSeverityBranches);
                    });
                });

                col.Item().PageBreak(); // SPLIT TO PAGE 2

                // PAGE 2: Top IP Addresses, Top Ports, and Security Guidelines
                col.Item().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(2).Text("Top 10 IP Address with High Risk Exposure").Bold().FontSize(10);
                        DrawTopHostsTable(c, data.TopHosts);
                    });
                    row.ConstantItem(10); // spacing
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(2).Text("Top 10 Port with High Risk Exposure").Bold().FontSize(10);
                        DrawTopPortsTable(c, data.TopPorts);
                    });
                });

                col.Item().PaddingTop(10).Column(c =>
                {
                    c.Item().PaddingBottom(2).Text("Security & Remediation Guidelines").Bold().FontSize(10);
                    c.Item().Border(1).BorderColor("#cbd5e1").Background("#f8fafc").Padding(8).Column(guide =>
                    {
                        guide.Item().Row(r =>
                        {
                            r.RelativeItem().Column(g =>
                            {
                                g.Item().Text("1. HIGH Severity Findings (Port 21, 22, 23, 3389, etc.):").Bold().FontSize(8).FontColor("#b4320a");
                                g.Item().PaddingLeft(5).Text("• Segera tutup akses publik (Block di level Firewall/Router).").FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                                g.Item().PaddingLeft(5).Text("• Gunakan VPN Enterprise atau IP Whitelisting jika port harus diakses.").FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                            });
                            r.ConstantItem(20);
                            r.RelativeItem().Column(g =>
                            {
                                g.Item().Text("2. MEDIUM Severity Findings (Port 80, 443, 8080, etc.):").Bold().FontSize(8).FontColor("#856404");
                                g.Item().PaddingLeft(5).Text("• Pastikan service web menggunakan sertifikat SSL/TLS valid (HTTPS).").FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                                g.Item().PaddingLeft(5).Text("• Lakukan update/patching web server berkala secara konsisten.").FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                            });
                        });
                        
                        guide.Item().PaddingTop(5).Text("Catatan Laporan:").Bold().FontSize(8).FontColor("#0f172a");
                        guide.Item().PaddingLeft(5).Text("• Seluruh detail data histori scan lengkap telah diekspor ke file Excel pendamping (.xlsx).").FontSize(7.5f).FontColor(Colors.Grey.Darken3);
                    });
                });
            });
        }

        private void DrawSummaryCard(RowDescriptor row, string label, string value, string colorHex)
        {
            row.RelativeItem().PaddingRight(5).Border(1).BorderColor("#e2e8f0").Background("#f8fafc").Padding(5).Column(c =>
            {
                c.Item().Text(label).FontSize(7).FontColor(Colors.Grey.Darken2);
                c.Item().Text(value).FontSize(14).FontColor(colorHex).Bold();
            });
        }

        private void DrawTopBranchesTable(ColumnDescriptor col, List<TopBranchReportDto> data)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(20);
                    columns.RelativeColumn();
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                });

                table.Header(header =>
                {
                    header.Cell().Background("#0f172a").Padding(3).Text("#").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Zone Name").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Open Ports").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Risk Score").FontColor(Colors.White).FontSize(8).Bold();
                });

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text((i + 1).ToString()).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.BranchName).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.OpenPortCount.ToString()).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.RiskScore.ToString()).FontSize(8);
                }
            });
        }

        private void DrawTopHostsTable(ColumnDescriptor col, List<TopHostReportDto> data)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(20);
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                });

                table.Header(header =>
                {
                    header.Cell().Background("#2c3e50").Padding(3).Text("#").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#2c3e50").Padding(3).Text("IP Address").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#2c3e50").Padding(3).Text("Zone Name").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#2c3e50").Padding(3).Text("Open Ports").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#2c3e50").Padding(3).Text("Risk Score").FontColor(Colors.White).FontSize(8).Bold();
                });

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text((i + 1).ToString()).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.IpAddress).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.BranchName).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.OpenPortCount.ToString()).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.RiskScore.ToString()).FontSize(8);
                }
            });
        }

        private void DrawTopPortsTable(ColumnDescriptor col, List<TopPortReportDto> data)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(20);
                    columns.ConstantColumn(40);
                    columns.RelativeColumn();
                    columns.ConstantColumn(50);
                    columns.ConstantColumn(50);
                });

                table.Header(header =>
                {
                    header.Cell().Background("#dc3545").Padding(3).Text("#").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#dc3545").Padding(3).Text("Port").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#dc3545").Padding(3).Text("Description").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#dc3545").Padding(3).Text("Severity").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#dc3545").Padding(3).Text("Frequency").FontColor(Colors.White).FontSize(8).Bold();
                });

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    string sevLower = item.Severity.ToLower();
                    string colorHex = sevLower == "high" ? "#dc3545" : sevLower == "medium" ? "#fd7e14" : "#28a745";

                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text((i + 1).ToString()).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.PortNumber.ToString()).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.PortDesc).FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.Severity).FontColor(colorHex).Bold().FontSize(8);
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.OpenCount.ToString()).FontSize(8);
                }
            });
        }



        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Page ").FontSize(8);
                x.CurrentPageNumber().FontSize(8);
                x.Span(" of ").FontSize(8);
                x.TotalPages().FontSize(8);
            });
        }

        private class TimeRangeInterval
        {
            public int Index { get; set; }
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
            public string SheetName { get; set; } = "";
            public string TempPath { get; set; } = "";
            public StreamWriter? Writer { get; set; }
        }

        private IEnumerable<Dictionary<string, object>> ReadJsonLines(string path)
        {
            using var reader = new StreamReader(path);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var row = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(line);
                if (row != null)
                {
                    yield return row;
                }
            }
        }

        private async Task StreamExcelReportAsync(AppDbContext dbContext, DateTime dateStart, DateTime dateEnd, string scanType, string search, string excelFilePath)
        {
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "V2_sp_GetScanHistoryReport";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.CommandTimeout = 7200;

            var pStart = cmd.CreateParameter();
            pStart.ParameterName = "@DateStart";
            pStart.Value = dateStart;
            cmd.Parameters.Add(pStart);

            var pEnd = cmd.CreateParameter();
            pEnd.ParameterName = "@DateEnd";
            pEnd.Value = dateEnd;
            cmd.Parameters.Add(pEnd);

            var pScanType = cmd.CreateParameter();
            pScanType.ParameterName = "@ScanType";
            pScanType.Value = scanType;
            cmd.Parameters.Add(pScanType);

            var pSearch = cmd.CreateParameter();
            pSearch.ParameterName = "@SearchTerm";
            pSearch.Value = search ?? "";
            cmd.Parameters.Add(pSearch);

            // Define weekly intervals based on DateStart and DateEnd
            var intervals = new List<TimeRangeInterval>();
            DateTime current = dateStart;
            int weekNum = 1;
            while (current < dateEnd)
            {
                DateTime next = current.AddDays(7);
                if (next > dateEnd) next = dateEnd;

                intervals.Add(new TimeRangeInterval
                {
                    Index = weekNum,
                    Start = current,
                    End = next,
                    SheetName = $"W{weekNum} ({current:ddMMM}-{next:ddMMM})",
                    TempPath = Path.Combine(Path.GetTempPath(), $"temp_sheet_{Guid.NewGuid()}.json")
                });

                weekNum++;
                current = next;
            }

            if (intervals.Count == 0)
            {
                intervals.Add(new TimeRangeInterval
                {
                    Index = 1,
                    Start = dateStart,
                    End = dateEnd,
                    SheetName = "Report",
                    TempPath = Path.Combine(Path.GetTempPath(), $"temp_sheet_{Guid.NewGuid()}.json")
                });
            }

            // Open temporary JSON writers
            foreach (var interval in intervals)
            {
                interval.Writer = new StreamWriter(interval.TempPath, false, Encoding.UTF8);
            }

            bool wasClosed = dbContext.Database.GetDbConnection().State == System.Data.ConnectionState.Closed;
            if (wasClosed)
            {
                await dbContext.Database.OpenConnectionAsync();
            }

            try
            {
                using var reader = await cmd.ExecuteReaderAsync();

                // Skip 5 result set pertama (Summary, TopBranches, TopHosts, TopPorts, TopHighSeverityBranches)
                for (int i = 0; i < 5; i++)
                {
                    await reader.NextResultAsync();
                }

                int scanDateOrdinal = reader.GetOrdinal("ScanDate");
                int branchNameOrdinal = reader.GetOrdinal("BranchName");
                int ipAddressOrdinal = reader.GetOrdinal("IpAddress");
                int hostStatusOrdinal = reader.GetOrdinal("HostStatus");
                int openPortsOrdinal = reader.GetOrdinal("OpenPorts");

                // Keep counters for No
                var rowCounters = new Dictionary<int, int>();
                foreach (var interval in intervals)
                {
                    rowCounters[interval.Index] = 1;
                }

                while (await reader.ReadAsync())
                {
                    DateTime scanDateVal = reader.GetDateTime(scanDateOrdinal);
                    
                    // Match interval (inclusive of Start, exclusive of End, except for the last one)
                    var interval = intervals.FirstOrDefault(i => scanDateVal >= i.Start && scanDateVal < i.End);
                    if (interval == null && scanDateVal == dateEnd)
                    {
                        interval = intervals.LastOrDefault();
                    }
                    if (interval == null)
                    {
                        interval = intervals.LastOrDefault() ?? intervals.First();
                    }

                    string scanDate = scanDateVal.ToString("yyyy-MM-dd HH:mm:ss");
                    string zone = reader.IsDBNull(branchNameOrdinal) ? "" : reader.GetString(branchNameOrdinal);
                    string ipAddress = reader.IsDBNull(ipAddressOrdinal) ? "" : reader.GetString(ipAddressOrdinal);
                    string statusHost = reader.GetBoolean(hostStatusOrdinal) ? "UP" : "DOWN";
                    
                    string rawPorts = reader.IsDBNull(openPortsOrdinal) ? "-" : reader.GetString(openPortsOrdinal);
                    string openPorts = FormatPortsForCsv(rawPorts);

                    int currentNo = rowCounters[interval.Index];

                    var row = new Dictionary<string, object>
                    {
                        ["No"] = currentNo,
                        ["Scan Date"] = scanDate,
                        ["Zone"] = zone,
                        ["IP Address"] = ipAddress,
                        ["Status Host"] = statusHost,
                        ["Open Ports"] = openPorts
                    };

                    await interval.Writer!.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(row));
                    rowCounters[interval.Index] = currentNo + 1;
                }

                // Close writers before reading them back
                foreach (var interval in intervals)
                {
                    if (interval.Writer != null)
                    {
                        await interval.Writer.DisposeAsync();
                        interval.Writer = null;
                    }
                }

                // Write to XLSX using MiniExcel
                var sheets = new Dictionary<string, object>();
                foreach (var interval in intervals)
                {
                    sheets.Add(interval.SheetName, ReadJsonLines(interval.TempPath));
                }

                MiniExcel.SaveAs(excelFilePath, sheets);
            }
            finally
            {
                // Ensure all writers are closed
                foreach (var interval in intervals)
                {
                    if (interval.Writer != null)
                    {
                        await interval.Writer.DisposeAsync();
                    }
                }

                if (wasClosed)
                {
                    await dbContext.Database.CloseConnectionAsync();
                }

                // Clean up all temporary files
                foreach (var interval in intervals)
                {
                    try
                    {
                        if (File.Exists(interval.TempPath))
                        {
                            File.Delete(interval.TempPath);
                        }
                    }
                    catch
                    {
                        // Ignore
                    }
                }
            }
        }

        private string FormatPortsForCsv(string portsStr)
        {
            if (string.IsNullOrWhiteSpace(portsStr) || portsStr == "-")
                return "-";

            var ports = portsStr.Split(',')
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(p => {
                    var parts = p.Split('|');
                    return parts[0];
                });

            return string.Join(", ", ports);
        }
    }
}
