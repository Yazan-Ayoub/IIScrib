using IIScribe.Core.Enums;
using IIScribe.Core.Entities;

namespace IIScribe.Core.DTOs;

/// <summary>
/// Request to deploy an application
/// </summary>
public record DeploymentRequest
{
    public string ApplicationPath { get; init; } = string.Empty;
    public string? ProfileId { get; init; }
    
    // Override settings (if not using profile)
    public string? DomainName { get; init; }
    public int? HttpPort { get; init; }
    public int? HttpsPort { get; init; }
    public DeploymentTarget? Target { get; init; }
    public DeploymentEnvironment? Environment { get; init; }
    public DeploymentStrategy? Strategy { get; init; }
    
    public DatabaseConfiguration? DatabaseConfig { get; init; }
    public SslConfiguration? SslConfig { get; init; }
    public CloudConfiguration? CloudConfig { get; init; }
    
    public bool RunHealthChecks { get; init; } = true;
    public bool SendNotifications { get; init; } = true;
    
    public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
}

/// <summary>
/// Result of a deployment operation
/// </summary>
public record DeploymentResult
{
    public bool Success { get; init; }
    public Guid DeploymentId { get; init; }
    public string Url { get; init; } = string.Empty;
    public int DurationSeconds { get; init; }
    public DeploymentStatus Status { get; init; }
    
    public DatabaseDeploymentResult? DatabaseResult { get; init; }
    public CertificateResult? CertificateResult { get; init; }
    public HealthCheckSummary? HealthCheckSummary { get; init; }
    
    public string? ErrorMessage { get; init; }
    public string? RollbackCommand { get; init; }
    
    public Dictionary<string, object> Metadata { get; init; } = new();
}

/// <summary>
/// Database deployment result
/// </summary>
public record DatabaseDeploymentResult
{
    public bool Success { get; init; }
    public string DatabaseName { get; init; } = string.Empty;
    public string ServerName { get; init; } = string.Empty;
    public int ScriptsExecuted { get; init; }
    public int RowsAffected { get; init; }
    public string? BackupPath { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Certificate operation result
/// </summary>
public record CertificateResult
{
    public bool Success { get; init; }
    public string Thumbprint { get; init; } = string.Empty;
    public CertificateType Type { get; init; }
    public DateTime ExpiryDate { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Health check result
/// </summary>
public record HealthCheckResult
{
    public string CheckName { get; init; } = string.Empty;
    public HealthCheckStatus Status { get; init; }
    public string? Message { get; init; }
    public int ResponseTimeMs { get; init; }
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;
    public Dictionary<string, object> Data { get; init; } = new();
}

/// <summary>
/// Summary of all health checks
/// </summary>
public record HealthCheckSummary
{
    public bool AllHealthy { get; init; }
    public int TotalChecks { get; init; }
    public int HealthyCount { get; init; }
    public int DegradedCount { get; init; }
    public int UnhealthyCount { get; init; }
    public IEnumerable<HealthCheckResult> Results { get; init; } = Array.Empty<HealthCheckResult>();
}

/// <summary>
/// Progress information for long-running operations
/// </summary>
public record ProgressInfo
{
    public string Stage { get; init; } = string.Empty;
    public int PercentComplete { get; init; }
    public string Message { get; init; } = string.Empty;
    public LogLevel Level { get; init; } = LogLevel.Information;
}

/// <summary>
/// IIS Application Pool configuration
/// </summary>
public record AppPoolConfiguration
{
    public string Name { get; init; } = string.Empty;
    public AppPoolRuntimeVersion RuntimeVersion { get; init; }
    public PipelineMode PipelineMode { get; init; }
    public bool Enable32BitAppOnWin64 { get; init; }
    public int IdleTimeoutMinutes { get; init; } = 20;
    public bool AlwaysRunning { get; init; }
    public string? Identity { get; init; }
}

/// <summary>
/// IIS Website configuration
/// </summary>
public record WebsiteConfiguration
{
    public string Name { get; init; } = string.Empty;
    public string PhysicalPath { get; init; } = string.Empty;
    public string AppPoolName { get; init; } = string.Empty;
    public string DomainName { get; init; } = string.Empty;
    public int HttpPort { get; init; } = 80;
    public int HttpsPort { get; init; } = 443;
    public bool EnableHttps { get; init; } = true;
    public string? CertificateThumbprint { get; init; }
}

/// <summary>
/// Application deployment configuration
/// </summary>
public record ApplicationDeploymentConfig
{
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public string SiteName { get; init; } = string.Empty;
    public bool StopSiteBeforeDeployment { get; init; } = true;
    public bool BackupExisting { get; init; } = true;
    public IEnumerable<string> ExcludePatterns { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Backup operation result
/// </summary>
public record BackupResult
{
    public bool Success { get; init; }
    public string BackupPath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string? Checksum { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Certificate information
/// </summary>
public record CertificateInfo
{
    public string Thumbprint { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public string IssuerName { get; init; } = string.Empty;
    public DateTime NotBefore { get; init; }
    public DateTime NotAfter { get; init; }
    public int DaysUntilExpiry { get; init; }
    public bool IsSelfSigned { get; init; }
}

/// <summary>
/// Cloud environment detection result
/// </summary>
public record CloudEnvironmentInfo
{
    public CloudProvider Provider { get; init; }
    public bool IsCloud { get; init; }
    public string? VmName { get; init; }
    public string? Region { get; init; }
    public string? InstanceId { get; init; }
    public string? PublicIp { get; init; }
    public string? PrivateIp { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = new();
}

/// <summary>
/// Azure VM provisioning configuration
/// </summary>
public record AzureVMConfiguration
{
    public string VmName { get; init; } = string.Empty;
    public string ResourceGroup { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string VmSize { get; init; } = "Standard_B2s";
    public string AdminUsername { get; init; } = string.Empty;
    public string AdminPassword { get; init; } = string.Empty;
    public bool AssignPublicIp { get; init; } = true;
    public IEnumerable<int> OpenPorts { get; init; } = new[] { 80, 443 };
}

/// <summary>
/// Deployment status with additional context
/// </summary>
public record DeploymentStatusInfo
{
    public Guid DeploymentId { get; init; }
    public DeploymentStatus Status { get; init; }
    public int PercentComplete { get; init; }
    public string CurrentStage { get; init; } = string.Empty;
    public DateTime? StartedAt { get; init; }
    public DateTime? EstimatedCompletion { get; init; }
    public IEnumerable<string> RecentLogs { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Dashboard summary
/// </summary>
public record DashboardSummary
{
    public int TotalDeployments { get; init; }
    public int ActiveSites { get; init; }
    public double SuccessRate { get; init; }
    public int DeploymentsLast30Days { get; init; }
    public int CertificatesExpiringSoon { get; init; }
    public IEnumerable<SiteStatus> RecentSites { get; init; } = Array.Empty<SiteStatus>();
    public IEnumerable<Deployment> RecentDeployments { get; init; } = Array.Empty<Deployment>();
}

/// <summary>
/// Team metrics for analytics
/// </summary>
public record TeamMetrics
{
    public string TeamName { get; init; } = string.Empty;
    public int TotalDeployments { get; init; }
    public double SuccessRate { get; init; }
    public int AverageDeploymentTimeSeconds { get; init; }
    public Dictionary<string, int> DeploymentsByUser { get; init; } = new();
    public Dictionary<string, int> DeploymentsByEnvironment { get; init; } = new();
    public IEnumerable<string> MostCommonErrors { get; init; } = Array.Empty<string>();
}
