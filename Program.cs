using Microsoft.EntityFrameworkCore;
using portscanner_backend.Data;
using portscanner_backend.Services;
using portscanner_backend.Workers;

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

// Ensure wwwroot/reports exists so UseStaticFiles works even on first run
var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (!Directory.Exists(Path.Combine(webRootPath, "reports")))
{
    Directory.CreateDirectory(Path.Combine(webRootPath, "reports"));
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
app.UseStaticFiles(); // Enable serving PDF files from wwwroot

app.UseAuthorization();

app.MapControllers();

app.Run();
