using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Services;
using portscanner_backend.Workers;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Daftarkan DbContext dengan SQL Server
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

builder.Services.AddControllers();
builder.Services.AddScoped<PortScanService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<ReportGeneratorService>();
builder.Services.AddHostedService<ScheduledScanWorker>();
builder.Services.AddHostedService<ReportSchedulerService>();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddSwaggerGen();

// Ensure the reports output directory exists
var outputPath = builder.Configuration["ReportSettings:OutputPath"] ?? "../Reports";
var absoluteOutputPath = Path.IsPathRooted(outputPath)
    ? outputPath
    : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), outputPath));

if (!Directory.Exists(absoluteOutputPath))
{
    Directory.CreateDirectory(absoluteOutputPath);
}

var app = builder.Build();

// Clean up stale "generating" report records from the database on startup (e.g. after a crash or server restart)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        var staleReports = context.GeneratedReports.Where(r => r.FilePath == "generating").ToList();
        if (staleReports.Any())
        {
            context.GeneratedReports.RemoveRange(staleReports);
            context.SaveChanges();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation($"[STARTUP] Berhasil membersihkan {staleReports.Count} data report menggantung yang berstatus 'generating'.");
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "[STARTUP] Gagal membersihkan report menggantung.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseCors("AllowReactApp");
app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(absoluteOutputPath),
    RequestPath = "/reports"
});

app.UseAuthorization();

app.MapControllers();

app.Run();
