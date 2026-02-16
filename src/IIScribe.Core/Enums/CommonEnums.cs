namespace IIScribe.Core.Enums;

/// <summary>
/// Target environment for deployment
/// </summary>
public enum DeploymentEnvironment
{
    Development,
    Staging,
    Production,
    Testing,
    Local
}

/// <summary>
/// Type of application being deployed
/// </summary>
public enum ApplicationType
{
    AspNetCoreMvc,
    AspNetCoreRazor,
    AspNetCoreBlazorServer,
    AspNetCoreBlazorWasm,
    AspNetCoreWebApi,
    AspNetFrameworkMvc,
    AspNetFrameworkWebForms,
    StaticWebsite,
    NodeJs,
    Unknown
}

/// <summary>
/// Deployment target platform
/// </summary>
public enum DeploymentTarget
{
    LocalIIS,
    LocalIISExpress,
    AzureAppService,
    AzureVM,
    AWSEC2,
    GoogleCloudVM,
    OnPremiseWindows,
    Docker,
    Kubernetes
}

/// <summary>
/// Database provider type
/// </summary>
public enum DatabaseProvider
{
    None,
    SqlServerLocalDb,
    SqlServerExpress,
    SqlServer,
    AzureSqlDatabase,
    PostgreSQL,
    MySQL,
    SQLite,
    MongoDB,
    CosmosDB
}

/// <summary>
/// Database deployment mode
/// </summary>
public enum DatabaseDeploymentMode
{
    /// <summary>
    /// Drop and recreate database (development only)
    /// </summary>
    Fresh,
    
    /// <summary>
    /// Apply migrations to existing database
    /// </summary>
    Migrate,
    
    /// <summary>
    /// Generate diff script and preview before applying
    /// </summary>
    SchemaCompare,
    
    /// <summary>
    /// Deploy using DACPAC
    /// </summary>
    DacPac,
    
    /// <summary>
    /// No database deployment
    /// </summary>
    None
}

/// <summary>
/// SSL certificate type
/// </summary>
public enum CertificateType
{
    SelfSigned,
    LetsEncrypt,
    InternalCA,
    AzureKeyVault,
    CustomCertificate
}

/// <summary>
/// Deployment status
/// </summary>
public enum DeploymentStatus
{
    Pending,
    InProgress,
    ValidationFailed,
    BackupInProgress,
    DatabaseDeploying,
    AppDeploying,
    ConfiguringSSL,
    RunningHealthChecks,
    Success,
    Failed,
    RolledBack,
    PartialSuccess
}

/// <summary>
/// Deployment strategy
/// </summary>
public enum DeploymentStrategy
{
    /// <summary>
    /// Stop site, deploy, start site
    /// </summary>
    StopAndDeploy,
    
    /// <summary>
    /// Blue-green deployment
    /// </summary>
    BlueGreen,
    
    /// <summary>
    /// Gradual traffic shift
    /// </summary>
    Canary,
    
    /// <summary>
    /// Rolling update
    /// </summary>
    Rolling,
    
    /// <summary>
    /// In-place update without downtime
    /// </summary>
    InPlace
}

/// <summary>
/// Authentication mode for database
/// </summary>
public enum DatabaseAuthenticationMode
{
    WindowsIntegrated,
    SqlAuthentication,
    AzureManagedIdentity,
    ConnectionString
}

/// <summary>
/// Log level for deployment operations
/// </summary>
public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Cloud provider
/// </summary>
public enum CloudProvider
{
    None,
    Azure,
    AWS,
    GoogleCloud
}

/// <summary>
/// IIS application pool managed runtime version
/// </summary>
public enum AppPoolRuntimeVersion
{
    NoManagedCode,
    V2_0,
    V4_0
}

/// <summary>
/// IIS application pool pipeline mode
/// </summary>
public enum PipelineMode
{
    Classic,
    Integrated
}

/// <summary>
/// Notification channel type
/// </summary>
public enum NotificationChannel
{
    None,
    Email,
    Slack,
    MicrosoftTeams,
    Webhook
}

/// <summary>
/// Audit event type
/// </summary>
public enum AuditEventType
{
    DeploymentStarted,
    DeploymentCompleted,
    DeploymentFailed,
    DeploymentRolledBack,
    ConfigurationChanged,
    UserLoggedIn,
    UserLoggedOut,
    PermissionGranted,
    PermissionRevoked,
    BackupCreated,
    BackupRestored,
    CertificateInstalled,
    CertificateRenewed,
    SecurityAlert
}

/// <summary>
/// User role for access control
/// </summary>
public enum UserRole
{
    Administrator,
    TeamLead,
    Developer,
    DevOpsEngineer,
    DatabaseAdministrator,
    SecurityOfficer,
    Viewer
}

/// <summary>
/// Backup retention policy
/// </summary>
public enum BackupRetentionPolicy
{
    KeepLast5,
    KeepLast10,
    KeepDaily7Days,
    KeepWeekly30Days,
    KeepMonthly1Year,
    Custom
}

/// <summary>
/// Health check status
/// </summary>
public enum HealthCheckStatus
{
    Healthy,
    Degraded,
    Unhealthy,
    Unknown
}
