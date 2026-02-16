using IIScribe.Core.Enums;

namespace IIScribe.Core.Entities;

/// <summary>
/// Reusable deployment profile/template
/// </summary>
public class DeploymentProfile : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Active"; // Active, Archived, Template
    
    // Application
    public ApplicationType ApplicationType { get; set; }
    public DeploymentTarget Target { get; set; }
    public DeploymentEnvironment Environment { get; set; }
    public DeploymentStrategy Strategy { get; set; }
    
    // Domain Configuration
    public string DomainPattern { get; set; } = string.Empty; // e.g., "{appname}.{env}.local"
    public int HttpPort { get; set; } = 80;
    public int HttpsPort { get; set; } = 443;
    
    // IIS Settings
    public AppPoolRuntimeVersion RuntimeVersion { get; set; }
    public PipelineMode PipelineMode { get; set; }
    public int AppPoolIdleTimeoutMinutes { get; set; } = 20;
    public bool AppPoolAlwaysRunning { get; set; }
    
    // Database Template
    public DatabaseConfiguration? DatabaseTemplate { get; set; }
    
    // SSL Template
    public SslConfiguration? SslTemplate { get; set; }
    
    // Cloud Template
    public CloudConfiguration? CloudTemplate { get; set; }
    
    // Environment Variables
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    
    // Custom Settings
    public Dictionary<string, string> CustomSettings { get; set; } = new();
    
    // Team/Client Association
    public string? TeamName { get; set; }
    public string? ClientName { get; set; }
    
    // Usage Tracking
    public int DeploymentCount { get; set; }
    public DateTime? LastUsedAt { get; set; }
    
    // Sharing
    public bool IsShared { get; set; }
    public bool IsTemplate { get; set; }
    
    // Deployments using this profile
    public ICollection<Deployment> Deployments { get; set; } = new List<Deployment>();
}

/// <summary>
/// Application discovery result
/// </summary>
public class ApplicationDiscovery : BaseEntity
{
    public string Path { get; set; } = string.Empty;
    public ApplicationType DetectedType { get; set; }
    public string FrameworkVersion { get; set; } = string.Empty;
    
    // Project Files
    public string? ProjectFilePath { get; set; }
    public string? SolutionFilePath { get; set; }
    
    // Configuration Files
    public List<string> ConfigFiles { get; set; } = new();
    public List<string> ConnectionStrings { get; set; } = new();
    
    // Database Scripts
    public string? DatabaseScriptsFolder { get; set; }
    public List<string> SqlScripts { get; set; } = new();
    public string? MigrationsFolder { get; set; }
    
    // Dependencies
    public List<string> NuGetPackages { get; set; } = new();
    public List<string> RequiredFeatures { get; set; } = new();
    
    // Size
    public long TotalSizeBytes { get; set; }
    public int FileCount { get; set; }
    
    // Recommendations
    public ApplicationType RecommendedType { get; set; }
    public DeploymentTarget RecommendedTarget { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Site status information
/// </summary>
public class SiteStatus : BaseEntity
{
    public string SiteName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsRunning { get; set; }
    public string State { get; set; } = string.Empty; // Started, Stopped, Starting, Stopping
    
    public string AppPoolName { get; set; } = string.Empty;
    public bool AppPoolRunning { get; set; }
    
    // Resource Usage
    public long WorkingSetMemoryMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public int ActiveConnections { get; set; }
    public int TotalRequests { get; set; }
    
    // Last Deployment
    public Guid? LastDeploymentId { get; set; }
    public DateTime? LastDeployedAt { get; set; }
    public string? LastDeployedBy { get; set; }
    
    // Health
    public HealthCheckStatus Health { get; set; }
    public DateTime? LastHealthCheckAt { get; set; }
    
    // Certificate
    public string? CertificateThumbprint { get; set; }
    public DateTime? CertificateExpiryDate { get; set; }
    public int? CertificateDaysUntilExpiry { get; set; }
    
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
}

/// <summary>
/// Notification configuration
/// </summary>
public class NotificationConfiguration : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public bool Enabled { get; set; } = true;
    
    // Events to notify
    public bool NotifyOnStart { get; set; } = true;
    public bool NotifyOnSuccess { get; set; } = true;
    public bool NotifyOnFailure { get; set; } = true;
    public bool NotifyOnRollback { get; set; } = true;
    
    // Email
    public string? EmailTo { get; set; }
    public string? EmailFrom { get; set; }
    public string? SmtpServer { get; set; }
    public int? SmtpPort { get; set; }
    
    // Slack
    public string? SlackWebhookUrl { get; set; }
    public string? SlackChannel { get; set; }
    
    // Teams
    public string? TeamsWebhookUrl { get; set; }
    
    // Custom Webhook
    public string? WebhookUrl { get; set; }
    public Dictionary<string, string> WebhookHeaders { get; set; } = new();
}

/// <summary>
/// User for access control
/// </summary>
public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    
    public string? FullName { get; set; }
    public string? TeamName { get; set; }
    
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    
    // API Access
    public string? ApiKey { get; set; }
    public DateTime? ApiKeyExpiresAt { get; set; }
    
    // Preferences
    public Dictionary<string, string> Preferences { get; set; } = new();
}

/// <summary>
/// Backup record
/// </summary>
public class Backup : BaseEntity
{
    public Guid DeploymentId { get; set; }
    public Deployment? Deployment { get; set; }
    
    public string BackupType { get; set; } = string.Empty; // Database, Application, Full
    public string FilePath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    
    public DateTime BackupDate { get; set; } = DateTime.UtcNow;
    public DateTime ExpiryDate { get; set; }
    
    public bool IsRestored { get; set; }
    public DateTime? RestoredAt { get; set; }
    
    // Verification
    public bool Verified { get; set; }
    public string? Checksum { get; set; }
}
