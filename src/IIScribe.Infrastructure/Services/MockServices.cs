using IIScribe.Core.DTOs;
using IIScribe.Core.Entities;
using IIScribe.Core.Enums;
using IIScribe.Core.Interfaces;
using LogLevel = IIScribe.Core.Enums.LogLevel;

namespace IIScribe.Infrastructure.Services;

// Mock implementations for testing - replace with real implementations in production

public class MockApplicationDiscoveryService : IApplicationDiscoveryService
{
    public Task<ApplicationDiscovery> DiscoverAsync(string path)
    {
        return Task.FromResult(new ApplicationDiscovery
        {
            Path = path,
            DetectedType = ApplicationType.AspNetCoreMvc,
            FrameworkVersion = "net8.0",
            ProjectFilePath = Path.Combine(path, "MyApp.csproj"),
            Warnings = new List<string>(),
            Recommendations = new List<string> { "Application looks good!" }
        });
    }

    public Task<ApplicationType> DetectTypeAsync(string path) => 
        Task.FromResult(ApplicationType.AspNetCoreMvc);

    public Task<IEnumerable<string>> FindDatabaseScriptsAsync(string path) => 
        Task.FromResult(Enumerable.Empty<string>());

    public Task<IEnumerable<string>> FindConnectionStringsAsync(string path) => 
        Task.FromResult(Enumerable.Empty<string>());
}

public class MockDatabaseDeploymentService : IDatabaseDeploymentService
{
    public Task<bool> DatabaseExistsAsync(DatabaseConfiguration config) => 
        Task.FromResult(false);

    public Task CreateDatabaseAsync(DatabaseConfiguration config, IProgress<ProgressInfo>? progress = null)
    {
        progress?.Report(new ProgressInfo
        {
            Stage = "Creating Database",
            PercentComplete = 100,
            Message = $"Database {config.DatabaseName} created successfully"
        });
        return Task.CompletedTask;
    }

    public Task<BackupResult> BackupDatabaseAsync(DatabaseConfiguration config, string backupPath)
    {
        return Task.FromResult(new BackupResult
        {
            Success = true,
            BackupPath = backupPath,
            FileSizeBytes = 1024 * 1024
        });
    }

    public Task RestoreDatabaseAsync(DatabaseConfiguration config, string backupPath) => 
        Task.CompletedTask;

    public Task RunScriptsAsync(DatabaseConfiguration config, IEnumerable<string> scripts, IProgress<ProgressInfo>? progress = null)
    {
        progress?.Report(new ProgressInfo
        {
            Stage = "Running Scripts",
            PercentComplete = 100,
            Message = $"Executed {scripts.Count()} scripts"
        });
        return Task.CompletedTask;
    }

    public Task RunMigrationsAsync(DatabaseConfiguration config, string migrationsFolder) => 
        Task.CompletedTask;

    public Task<bool> TestConnectionAsync(DatabaseConfiguration config) => 
        Task.FromResult(true);

    public Task UpdateConnectionStringAsync(string configFilePath, string connectionString) => 
        Task.CompletedTask;
}

public class MockCertificateService : ICertificateService
{
    public Task<CertificateResult> GenerateSelfSignedAsync(string domainName, int validityDays)
    {
        return Task.FromResult(new CertificateResult
        {
            Success = true,
            Thumbprint = "AA:BB:CC:DD:EE:FF:00:11:22:33:44:55:66:77:88:99",
            Type = CertificateType.SelfSigned,
            ExpiryDate = DateTime.UtcNow.AddDays(validityDays)
        });
    }

    public Task<CertificateResult> RequestLetsEncryptAsync(string domainName, string email)
    {
        return Task.FromResult(new CertificateResult
        {
            Success = true,
            Thumbprint = "11:22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00",
            Type = CertificateType.LetsEncrypt,
            ExpiryDate = DateTime.UtcNow.AddDays(90)
        });
    }

    public Task<CertificateResult> InstallCertificateAsync(string certificatePath, string password)
    {
        return Task.FromResult(new CertificateResult
        {
            Success = true,
            Thumbprint = "22:33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11",
            Type = CertificateType.CustomCertificate,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });
    }

    public Task<CertificateResult> GetFromKeyVaultAsync(string keyVaultUrl, string certificateName)
    {
        return Task.FromResult(new CertificateResult
        {
            Success = true,
            Thumbprint = "33:44:55:66:77:88:99:AA:BB:CC:DD:EE:FF:00:11:22",
            Type = CertificateType.AzureKeyVault,
            ExpiryDate = DateTime.UtcNow.AddYears(1)
        });
    }

    public Task InstallCertificateToIISAsync(string siteName, string thumbprint) => 
        Task.CompletedTask;

    public Task<IEnumerable<CertificateInfo>> GetExpiringCertificatesAsync(int daysThreshold) => 
        Task.FromResult(Enumerable.Empty<CertificateInfo>());

    public Task RenewCertificateAsync(string thumbprint) => 
        Task.CompletedTask;
}

public class MockHealthCheckService : IHealthCheckService
{
    public Task<HealthCheckResult> CheckHttpEndpointAsync(string url, int timeoutSeconds = 30)
    {
        return Task.FromResult(new HealthCheckResult
        {
            CheckName = "HTTP Endpoint",
            Status = HealthCheckStatus.Healthy,
            Message = "Endpoint responding normally",
            ResponseTimeMs = 45
        });
    }

    public Task<HealthCheckResult> CheckHttpsAsync(string url)
    {
        return Task.FromResult(new HealthCheckResult
        {
            CheckName = "HTTPS",
            Status = HealthCheckStatus.Healthy,
            Message = "SSL certificate valid",
            ResponseTimeMs = 52
        });
    }

    public Task<HealthCheckResult> CheckDatabaseAsync(DatabaseConfiguration config)
    {
        return Task.FromResult(new HealthCheckResult
        {
            CheckName = "Database",
            Status = HealthCheckStatus.Healthy,
            Message = "Database connection successful",
            ResponseTimeMs = 15
        });
    }

    public Task<HealthCheckResult> CheckCertificateAsync(string thumbprint)
    {
        return Task.FromResult(new HealthCheckResult
        {
            CheckName = "Certificate",
            Status = HealthCheckStatus.Healthy,
            Message = "Certificate valid for 364 days",
            ResponseTimeMs = 5
        });
    }

    public async Task<HealthCheckSummary> RunAllChecksAsync(Deployment deployment)
    {
        var results = new List<HealthCheckResult>
        {
            await CheckHttpEndpointAsync(deployment.TargetUrl),
            await CheckHttpsAsync(deployment.TargetUrl)
        };

        if (deployment.DatabaseConfig != null)
        {
            results.Add(await CheckDatabaseAsync(deployment.DatabaseConfig));
        }

        return new HealthCheckSummary
        {
            AllHealthy = results.All(r => r.Status == HealthCheckStatus.Healthy),
            TotalChecks = results.Count,
            HealthyCount = results.Count(r => r.Status == HealthCheckStatus.Healthy),
            DegradedCount = results.Count(r => r.Status == HealthCheckStatus.Degraded),
            UnhealthyCount = results.Count(r => r.Status == HealthCheckStatus.Unhealthy),
            Results = results
        };
    }
}

public class MockCloudDeploymentService : ICloudDeploymentService
{
    public Task<CloudEnvironmentInfo> DetectCloudEnvironmentAsync()
    {
        return Task.FromResult(new CloudEnvironmentInfo
        {
            Provider = CloudProvider.None,
            IsCloud = false
        });
    }

    public Task<string> ProvisionAzureVMAsync(AzureVMConfiguration config, IProgress<ProgressInfo>? progress = null)
    {
        progress?.Report(new ProgressInfo
        {
            Stage = "Provisioning VM",
            PercentComplete = 100,
            Message = "VM created successfully"
        });
        return Task.FromResult(config.VmName);
    }

    public Task UpdateDnsAsync(CloudConfiguration config, string ipAddress) => 
        Task.CompletedTask;

    public Task<string> GetPublicIpAddressAsync() => 
        Task.FromResult("203.0.113.42");
}

public class MockNotificationService : INotificationService
{
    public Task SendDeploymentStartedAsync(Deployment deployment, NotificationConfiguration config) => 
        Task.CompletedTask;

    public Task SendDeploymentSuccessAsync(Deployment deployment, NotificationConfiguration config) => 
        Task.CompletedTask;

    public Task SendDeploymentFailedAsync(Deployment deployment, NotificationConfiguration config) => 
        Task.CompletedTask;

    public Task SendDeploymentRolledBackAsync(Deployment deployment, NotificationConfiguration config) => 
        Task.CompletedTask;
}

public class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    private readonly List<T> _data = new();

    public Task<T?> GetByIdAsync(Guid id) => 
        Task.FromResult(_data.FirstOrDefault(x => x.Id == id));

    public Task<IEnumerable<T>> GetAllAsync() => 
        Task.FromResult(_data.AsEnumerable());

    public Task<IEnumerable<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate) => 
        Task.FromResult(_data.AsQueryable().Where(predicate).AsEnumerable());

    public Task<T> AddAsync(T entity)
    {
        _data.Add(entity);
        return Task.FromResult(entity);
    }

    public Task UpdateAsync(T entity)
    {
        var existing = _data.FirstOrDefault(x => x.Id == entity.Id);
        if (existing != null)
        {
            var index = _data.IndexOf(existing);
            _data[index] = entity;
        }
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        var entity = _data.FirstOrDefault(x => x.Id == id);
        if (entity != null)
        {
            _data.Remove(entity);
        }
        return Task.CompletedTask;
    }

    public Task<int> CountAsync() => 
        Task.FromResult(_data.Count);
}

public class MockAuditService : IAuditService
{
    public Task LogEventAsync(AuditEventType eventType, string userId, string action, string resourceId, Dictionary<string, object>? data = null)
    {
        Console.WriteLine($"[AUDIT] {eventType} by {userId}: {action} on {resourceId}");
        return Task.CompletedTask;
    }

    public Task<IEnumerable<AuditLog>> GetAuditTrailAsync(string resourceId) => 
        Task.FromResult(Enumerable.Empty<AuditLog>());

    public Task<IEnumerable<AuditLog>> GetUserActivityAsync(string userId, DateTime from, DateTime to) => 
        Task.FromResult(Enumerable.Empty<AuditLog>());
}

public class MockProfileService : IProfileService
{
    private readonly IRepository<DeploymentProfile> _repository;

    public MockProfileService(IRepository<DeploymentProfile> repository)
    {
        _repository = repository;
    }

    public async Task<DeploymentProfile> CreateProfileAsync(DeploymentProfile profile) => 
        await _repository.AddAsync(profile);

    public async Task<DeploymentProfile?> GetProfileAsync(Guid id) => 
        await _repository.GetByIdAsync(id);

    public async Task<IEnumerable<DeploymentProfile>> GetProfilesByCategoryAsync(string category) => 
        await _repository.FindAsync(p => p.Category == category);

    public async Task<IEnumerable<DeploymentProfile>> SearchProfilesAsync(string searchTerm)
    {
        var all = await _repository.GetAllAsync();
        return all.Where(p => p.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> ExportProfileAsync(Guid profileId)
    {
        var profile = await _repository.GetByIdAsync(profileId);
        return System.Text.Json.JsonSerializer.Serialize(profile);
    }

    public async Task<DeploymentProfile> ImportProfileAsync(string json)
    {
        var profile = System.Text.Json.JsonSerializer.Deserialize<DeploymentProfile>(json)!;
        profile.Id = Guid.NewGuid();
        return await _repository.AddAsync(profile);
    }
}

public class MockHostsFileService : IHostsFileService
{
    public Task AddEntryAsync(string ipAddress, string hostname)
    {
        Console.WriteLine($"[HOSTS] Added: {ipAddress} {hostname}");
        return Task.CompletedTask;
    }

    public Task RemoveEntryAsync(string hostname)
    {
        Console.WriteLine($"[HOSTS] Removed: {hostname}");
        return Task.CompletedTask;
    }

    public Task<bool> EntryExistsAsync(string hostname) => 
        Task.FromResult(false);

    public Task BackupAsync() => 
        Task.CompletedTask;

    public Task RestoreAsync() => 
        Task.CompletedTask;
}

public class MockEncryptionService : IEncryptionService
{
    public string Encrypt(string plainText) => 
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plainText));

    public string Decrypt(string cipherText) => 
        System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));

    public string HashPassword(string password) => 
        Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)));

    public bool VerifyPassword(string password, string hash) => 
        HashPassword(password) == hash;
}

public class MockDeploymentLogger : IDeploymentLogger
{
    private readonly List<DeploymentLog> _logs = new();

    public Task LogAsync(Guid deploymentId, LogLevel level, string message, Exception? exception = null)
    {
        var log = new DeploymentLog
        {
            DeploymentId = deploymentId,
            Level = level,
            Message = message,
            Exception = exception?.Message,
            StackTrace = exception?.StackTrace
        };
        _logs.Add(log);
        
        Console.WriteLine($"[{level}] {message}");
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<DeploymentLog>> GetLogsAsync(Guid deploymentId) => 
        Task.FromResult(_logs.Where(l => l.DeploymentId == deploymentId));
}
