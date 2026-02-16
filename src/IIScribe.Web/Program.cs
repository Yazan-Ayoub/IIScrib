using IIScribe.Core.Interfaces;
using IIScribe.Infrastructure.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/iiscribe-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "IIScribe API", 
        Version = "v3.0",
        Description = "Professional deployment orchestration platform for .NET applications"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

// Register application services
RegisterServices(builder.Services);

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseDefaultFiles(); // Serve index.html by default
app.UseStaticFiles();  // Serve static files from wwwroot

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "IIScribe API v3.0");
        c.RoutePrefix = "swagger"; // Swagger at /swagger
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Fallback to index.html for SPA routing
app.MapFallbackToFile("index.html");

// Startup banner
Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════════════════════╗
║                                                                           ║
║                            IIScribe v3.0                                  ║
║                                                                           ║
║                    🚀 DEPLOY  📊 ANALYZE  ☁️ MIGRATE                     ║
║                                                                           ║
║          From localhost to Azure in 60 seconds                            ║
║          From chaos to control in one dashboard                           ║
║                                                                           ║
╚═══════════════════════════════════════════════════════════════════════════╝
");

app.Run();

void RegisterServices(IServiceCollection services)
{
    // Core Orchestrator
    services.AddScoped<IDeploymentOrchestrator, DeploymentOrchestrator>();
    
    // ⚡ REAL Infrastructure Services - Actually deploy to IIS!
    services.AddScoped<IApplicationDiscoveryService, MockApplicationDiscoveryService>();
    services.AddScoped<IIISDeploymentService, RealIISDeploymentService>(); // ← REAL IIS deployment!
    services.AddScoped<IDatabaseDeploymentService, MockDatabaseDeploymentService>();
    services.AddScoped<ICertificateService, MockCertificateService>();
    services.AddScoped<ICloudDeploymentService, MockCloudDeploymentService>();
    services.AddScoped<IHealthCheckService, MockHealthCheckService>();
    services.AddScoped<INotificationService, MockNotificationService>();
    services.AddScoped<IAuditService, MockAuditService>();
    services.AddScoped<IProfileService, MockProfileService>();
    services.AddScoped<IHostsFileService, RealHostsFileService>(); // ← REAL hosts file editing!
    services.AddScoped<IEncryptionService, MockEncryptionService>();
    services.AddScoped<IDeploymentLogger, MockDeploymentLogger>();
    
    // Repositories (in-memory for demo)
    services.AddSingleton(typeof(IRepository<>), typeof(InMemoryRepository<>));
    
    Console.WriteLine("✓ Services registered - REAL IIS deployment enabled!");
}
