using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Models;
using portscanner_backend.Models.Dto;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace portscanner_backend.Services
{
    public class ReportGeneratorService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IWebHostEnvironment _env;

        public ReportGeneratorService(IServiceScopeFactory scopeFactory, IWebHostEnvironment env)
        {
            _scopeFactory = scopeFactory;
            _env = env;
            // Configure QuestPDF license (Community is free for small companies)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task GenerateReportAsync(DateTime dateStart, DateTime dateEnd, string reportTitle, string reportType)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 1. Fetch Data
            var data = await FetchReportDataAsync(dbContext, dateStart, dateEnd);

            // 2. Setup File Path
            string reportsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "reports");
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

            // 4. Save to DB
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

        private async Task<ScanReportResponseDto> FetchReportDataAsync(AppDbContext dbContext, DateTime dateStart, DateTime dateEnd)
        {
            var cmd = dbContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "V2_sp_GetScanHistoryReport";
            cmd.CommandType = System.Data.CommandType.StoredProcedure;

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
            pScanType.Value = "all";
            cmd.Parameters.Add(pScanType);

            var pSearch = cmd.CreateParameter();
            pSearch.ParameterName = "@SearchTerm";
            pSearch.Value = "";
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

            await reader.NextResultAsync();
            var details = new List<ScanHistoryDto>();
            while (await reader.ReadAsync())
                details.Add(new ScanHistoryDto
                {
                    ScanDate = reader.GetDateTime(reader.GetOrdinal("ScanDate")),
                    ScanTitle = reader.GetString(reader.GetOrdinal("ScanTitle")),
                    ScanType = reader.GetString(reader.GetOrdinal("ScanType")),
                    BranchName = reader.GetString(reader.GetOrdinal("BranchName")),
                    IpAddress = reader.GetString(reader.GetOrdinal("IpAddress")),
                    HostStatus = reader.GetBoolean(reader.GetOrdinal("HostStatus")),
                    OpenPorts = reader.IsDBNull(reader.GetOrdinal("OpenPorts")) ? "-" : reader.GetString(reader.GetOrdinal("OpenPorts"))
                });

            return new ScanReportResponseDto
            {
                Summary = summary,
                TopBranches = topBranches,
                TopHosts = topHosts,
                TopPorts = topPorts,
                DetailHistory = details
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
                // Summary Cards
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
                        c.Item().PaddingBottom(2).Text("Top 5 Branches with High Risk Exposure").Bold().FontSize(10);
                        DrawTopBranchesTable(c, data.TopBranches);
                    });
                    row.ConstantItem(10); // spacing
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(2).Text("Top 5 IP Address with High Risk Exposure").Bold().FontSize(10);
                        DrawTopHostsTable(c, data.TopHosts);
                    });
                });

                col.Item().PaddingTop(10).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().PaddingBottom(2).Text("Top 5 Port with High Risk Exposure").Bold().FontSize(10);
                        DrawTopPortsTable(c, data.TopPorts);
                    });
                    row.RelativeItem(); // empty space on right
                });

                col.Item().PageBreak(); // Details go to new page
                
                col.Item().PaddingBottom(5).Text("Scan History Detail").Bold().FontSize(12);
                DrawDetailHistoryTable(col, data.DetailHistory);
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

        private void DrawDetailHistoryTable(ColumnDescriptor col, List<ScanHistoryDto> data)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(25);
                    columns.ConstantColumn(100);
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                    columns.RelativeColumn();
                    columns.ConstantColumn(100);
                    columns.ConstantColumn(50);
                    columns.RelativeColumn();
                });

                table.Header(header =>
                {
                    header.Cell().Background("#0f172a").Padding(3).Text("No").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Scan Date").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Scan Title").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Scan Type").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Zone").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("IP Address").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Status Host").FontColor(Colors.White).FontSize(8).Bold();
                    header.Cell().Background("#0f172a").Padding(3).Text("Open Ports").FontColor(Colors.White).FontSize(8).Bold();
                });

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i];
                    bool isEven = i % 2 == 0;
                    string bgCol = isEven ? "#ffffff" : "#f8f9fa";

                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text((i + 1).ToString()).FontSize(8);
                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.ScanDate.ToString("dd MMM yyyy HH:mm")).FontSize(8);
                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.ScanTitle).FontSize(8);
                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.ScanType).FontSize(8);
                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.BranchName).FontSize(8);
                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.IpAddress).FontSize(8);
                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Text(item.HostStatus ? "UP" : "DOWN").FontColor(item.HostStatus ? "#198754" : Colors.Grey.Darken2).Bold().FontSize(8);
                    
                    // Render Colored Ports
                    table.Cell().Background(bgCol).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(3).Element(c => DrawPortsCell(c, item.OpenPorts));
                }
            });
        }

        private void DrawPortsCell(IContainer container, string portsString)
        {
            if (string.IsNullOrWhiteSpace(portsString) || portsString == "-")
            {
                container.Text("-").FontSize(8);
                return;
            }

            container.Text(text =>
            {
                var ports = portsString.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                for (int i = 0; i < ports.Count; i++)
                {
                    var p = ports[i];
                    var parts = p.Split('|');
                    string portNum = parts[0];
                    string severity = parts.Length > 1 ? parts[1].Trim().ToLower() : "low";

                    string colorHex = "#6c757d";
                    if (severity == "high") colorHex = "#dc3545";
                    else if (severity == "medium") colorHex = "#fd7e14";
                    else if (severity == "low") colorHex = "#28a745";

                    text.Span(portNum).FontColor(colorHex).Bold().FontSize(8);
                    if (i < ports.Count - 1)
                    {
                        text.Span(", ").FontColor(Colors.Grey.Medium).FontSize(8);
                    }
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
    }
}
