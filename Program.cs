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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// app.UseHttpsRedirection();

app.UseCors("AllowReactApp");
app.UseStaticFiles(); // Enable serving static files from wwwroot

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(absoluteOutputPath),
    RequestPath = "/reports"
});

app.UseAuthorization();

app.MapControllers();

app.Run();
