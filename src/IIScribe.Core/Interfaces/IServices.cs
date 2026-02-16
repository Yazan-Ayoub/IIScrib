using IIScribe.Core.DTOs;
using IIScribe.Core.Entities;
using IIScribe.Core.Enums;

namespace IIScribe.Core.Interfaces;

/// <summary>
/// Orchestrates the entire deployment workflow
/// </summary>
public interface IDeploymentOrchestrator
{
    Task<DeploymentResult> DeployAsync(DeploymentRequest request, CancellationToken cancellationToken = default);
    Task<DeploymentResult> RollbackAsync(Guid deploymentId, CancellationToken cancellationToken = default);
    Task<DeploymentStatus> GetStatusAsync(Guid deploymentId);
    Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync();
}

/// <summary>
/// Discovers and analyzes applications for deployment
/// </summary>
public interface IApplicationDiscoveryService
{
    Task<ApplicationDiscovery> DiscoverAsync(string path);
    Task<ApplicationType> DetectTypeAsync(string path);
    Task<IEnumerable<string>> FindDatabaseScriptsAsync(string path);
    Task<IEnumerable<string>> FindConnectionStringsAsync(string path);
}

/// <summary>
/// Manages IIS configuration and deployment
/// </summary>
public interface IIISDeploymentService
{
    Task<bool> IsIISInstalledAsync();
    Task InstallIISAsync(IProgress<ProgressInfo>? progress = null);
    Task<string> CreateApplicationPoolAsync(AppPoolConfiguration config);
    Task<string> CreateWebsiteAsync(WebsiteConfiguration config);
    Task DeployApplicationAsync(ApplicationDeploymentConfig config, IProgress<ProgressInfo>? progress = null);
    Task StartSiteAsync(string siteName);
    Task StopSiteAsync(string siteName);
    Task RemoveSiteAsync(string siteName);
    Task<IEnumerable<SiteStatus>> GetAllSitesAsync();
}

/// <summary>
/// Manages database operations
/// </summary>
public interface IDatabaseDeploymentService
{
    Task<bool> DatabaseExistsAsync(DatabaseConfiguration config);
    Task CreateDatabaseAsync(DatabaseConfiguration config, IProgress<ProgressInfo>? progress = null);
    Task<BackupResult> BackupDatabaseAsync(DatabaseConfiguration config, string backupPath);
    Task RestoreDatabaseAsync(DatabaseConfiguration config, string backupPath);
    Task RunScriptsAsync(DatabaseConfiguration config, IEnumerable<string> scripts, IProgress<ProgressInfo>? progress = null);
    Task RunMigrationsAsync(DatabaseConfiguration config, string migrationsFolder);
    Task<bool> TestConnectionAsync(DatabaseConfiguration config);
    Task UpdateConnectionStringAsync(string configFilePath, string connectionString);
}

/// <summary>
/// Manages SSL certificates
/// </summary>
public interface ICertificateService
{
    Task<CertificateResult> GenerateSelfSignedAsync(string domainName, int validityDays);
    Task<CertificateResult> RequestLetsEncryptAsync(string domainName, string email);
    Task<CertificateResult> InstallCertificateAsync(string certificatePath, string password);
    Task<CertificateResult> GetFromKeyVaultAsync(string keyVaultUrl, string certificateName);
    Task InstallCertificateToIISAsync(string siteName, string thumbprint);
    Task<IEnumerable<CertificateInfo>> GetExpiringCertificatesAsync(int daysThreshold);
    Task RenewCertificateAsync(string thumbprint);
}

/// <summary>
/// Manages cloud operations
/// </summary>
public interface ICloudDeploymentService
{
    Task<CloudEnvironmentInfo> DetectCloudEnvironmentAsync();
    Task<string> ProvisionAzureVMAsync(AzureVMConfiguration config, IProgress<ProgressInfo>? progress = null);
    Task UpdateDnsAsync(CloudConfiguration config, string ipAddress);
    Task<string> GetPublicIpAddressAsync();
}

/// <summary>
/// Manages health checks
/// </summary>
public interface IHealthCheckService
{
    Task<HealthCheckResult> CheckHttpEndpointAsync(string url, int timeoutSeconds = 30);
    Task<HealthCheckResult> CheckHttpsAsync(string url);
    Task<HealthCheckResult> CheckDatabaseAsync(DatabaseConfiguration config);
    Task<HealthCheckResult> CheckCertificateAsync(string thumbprint);
    Task<HealthCheckSummary> RunAllChecksAsync(Deployment deployment);
}

/// <summary>
/// Manages notifications
/// </summary>
public interface INotificationService
{
    Task SendDeploymentStartedAsync(Deployment deployment, NotificationConfiguration config);
    Task SendDeploymentSuccessAsync(Deployment deployment, NotificationConfiguration config);
    Task SendDeploymentFailedAsync(Deployment deployment, NotificationConfiguration config);
    Task SendDeploymentRolledBackAsync(Deployment deployment, NotificationConfiguration config);
}

/// <summary>
/// Repository pattern for data access
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate);
    Task<T> AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(Guid id);
    Task<int> CountAsync();
}

/// <summary>
/// Audit logging service
/// </summary>
public interface IAuditService
{
    Task LogEventAsync(AuditEventType eventType, string userId, string action, string resourceId, Dictionary<string, object>? data = null);
    Task<IEnumerable<AuditLog>> GetAuditTrailAsync(string resourceId);
    Task<IEnumerable<AuditLog>> GetUserActivityAsync(string userId, DateTime from, DateTime to);
}

/// <summary>
/// Profile management
/// </summary>
public interface IProfileService
{
    Task<DeploymentProfile> CreateProfileAsync(DeploymentProfile profile);
    Task<DeploymentProfile?> GetProfileAsync(Guid id);
    Task<IEnumerable<DeploymentProfile>> GetProfilesByCategoryAsync(string category);
    Task<IEnumerable<DeploymentProfile>> SearchProfilesAsync(string searchTerm);
    Task<string> ExportProfileAsync(Guid profileId);
    Task<DeploymentProfile> ImportProfileAsync(string json);
}

/// <summary>
/// Manages the hosts file
/// </summary>
public interface IHostsFileService
{
    Task AddEntryAsync(string ipAddress, string hostname);
    Task RemoveEntryAsync(string hostname);
    Task<bool> EntryExistsAsync(string hostname);
    Task BackupAsync();
    Task RestoreAsync();
}

/// <summary>
/// Encryption service for sensitive data
/// </summary>
public interface IEncryptionService
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
}

/// <summary>
/// Logging abstraction
/// </summary>
public interface IDeploymentLogger
{
    Task LogAsync(Guid deploymentId, LogLevel level, string message, Exception? exception = null);
    Task<IEnumerable<DeploymentLog>> GetLogsAsync(Guid deploymentId);
}
