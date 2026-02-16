using IIScribe.Core.Enums;

namespace IIScribe.Core.Entities;

/// <summary>
/// Base entity with common properties
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>
/// Represents a deployment operation
/// </summary>
public class Deployment : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Application Info
    public string ApplicationPath { get; set; } = string.Empty;
    public ApplicationType ApplicationType { get; set; }
    public string ApplicationVersion { get; set; } = string.Empty;
    
    // Target Configuration
    public DeploymentTarget Target { get; set; }
    public DeploymentEnvironment Environment { get; set; }
    public string TargetUrl { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public int HttpPort { get; set; } = 80;
    public int HttpsPort { get; set; } = 443;
    
    // IIS Configuration
    public string? SiteName { get; set; }
    public string? AppPoolName { get; set; }
    public AppPoolRuntimeVersion RuntimeVersion { get; set; }
    public PipelineMode PipelineMode { get; set; }
    
    // Database Configuration
    public DatabaseConfiguration? DatabaseConfig { get; set; }
    
    // SSL Configuration
    public SslConfiguration? SslConfig { get; set; }
    
    // Strategy
    public DeploymentStrategy Strategy { get; set; }
    
    // Status Tracking
    public DeploymentStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int DurationSeconds { get; set; }
    
    // Results
    public string? DeploymentOutput { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RollbackCommand { get; set; }
    
    // Relationships
    public Guid? ProfileId { get; set; }
    public DeploymentProfile? Profile { get; set; }
    
    public ICollection<DeploymentLog> Logs { get; set; } = new List<DeploymentLog>();
    public ICollection<HealthCheck> HealthChecks { get; set; } = new List<HealthCheck>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    
    // Metadata
    public Dictionary<string, string> Metadata { get; set; } = new();
    
    // Cloud specific
    public CloudConfiguration? CloudConfig { get; set; }
}

/// <summary>
/// Database configuration for deployment
/// </summary>
public class DatabaseConfiguration
{
    public DatabaseProvider Provider { get; set; }
    public DatabaseDeploymentMode DeploymentMode { get; set; }
    public DatabaseAuthenticationMode AuthMode { get; set; }
    
    public string? ServerName { get; set; }
    public string? DatabaseName { get; set; }
    public int? Port { get; set; }
    
    // Authentication
    public string? Username { get; set; }
    public string? PasswordEncrypted { get; set; }
    
    // Connection String
    public string? ConnectionString { get; set; }
    
    // Scripts
    public List<string> ScriptPaths { get; set; } = new();
    public string? MigrationsFolder { get; set; }
    public string? DacPacPath { get; set; }
    
    // Backup
    public bool BackupBeforeDeployment { get; set; } = true;
    public string? BackupPath { get; set; }
    public BackupRetentionPolicy RetentionPolicy { get; set; }
    
    // Rollback
    public bool AutoRollbackOnFailure { get; set; } = true;
    
    // Seeding
    public bool SeedTestData { get; set; }
    public List<string> SeedScripts { get; set; } = new();
}

/// <summary>
/// SSL certificate configuration
/// </summary>
public class SslConfiguration
{
    public CertificateType CertificateType { get; set; }
    public bool EnableHsts { get; set; } = true;
    public bool RedirectHttpToHttps { get; set; } = true;
    
    // Self-signed
    public int ValidityDays { get; set; } = 365;
    
    // Let's Encrypt
    public string? LetsEncryptEmail { get; set; }
    public bool AutoRenew { get; set; } = true;
    
    // Custom Certificate
    public string? CertificatePath { get; set; }
    public string? CertificatePassword { get; set; }
    
    // Azure Key Vault
    public string? KeyVaultUrl { get; set; }
    public string? CertificateName { get; set; }
    
    // Certificate Info
    public DateTime? ExpiryDate { get; set; }
    public string? Thumbprint { get; set; }
}

/// <summary>
/// Cloud provider configuration
/// </summary>
public class CloudConfiguration
{
    public CloudProvider Provider { get; set; }
    
    // Azure
    public string? SubscriptionId { get; set; }
    public string? ResourceGroup { get; set; }
    public string? Location { get; set; }
    
    // AWS
    public string? AwsRegion { get; set; }
    public string? AwsAccessKeyId { get; set; }
    public string? AwsSecretKeyEncrypted { get; set; }
    
    // GCP
    public string? GcpProjectId { get; set; }
    public string? GcpServiceAccountKey { get; set; }
    
    // VM Configuration
    public string? VmName { get; set; }
    public string? VmSize { get; set; }
    public string? PublicIpAddress { get; set; }
    
    // DNS
    public bool ManageDns { get; set; }
    public string? DnsZone { get; set; }
    public string? DnsRecordName { get; set; }
}

/// <summary>
/// Deployment log entry
/// </summary>
public class DeploymentLog : BaseEntity
{
    public Guid DeploymentId { get; set; }
    public Deployment? Deployment { get; set; }
    
    public LogLevel Level { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
    public string? StackTrace { get; set; }
    
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheck : BaseEntity
{
    public Guid DeploymentId { get; set; }
    public Deployment? Deployment { get; set; }
    
    public string CheckName { get; set; } = string.Empty;
    public HealthCheckStatus Status { get; set; }
    public string? Message { get; set; }
    public int ResponseTimeMs { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    
    public Dictionary<string, object> Data { get; set; } = new();
}

/// <summary>
/// Audit log for security and compliance
/// </summary>
public class AuditLog : BaseEntity
{
    public AuditEventType EventType { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    
    public string? BeforeValue { get; set; }
    public string? AfterValue { get; set; }
    
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Cryptographic signature for tamper-proof logs
    public string? Signature { get; set; }
    
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}
