using IIScribe.Core.DTOs;
using IIScribe.Core.Entities;
using IIScribe.Core.Enums;
using IIScribe.Core.Interfaces;
using System.Diagnostics;
using LogLevel = IIScribe.Core.Enums.LogLevel;

namespace IIScribe.Infrastructure.Services;

/// <summary>
/// Orchestrates the complete deployment workflow
/// </summary>
public class DeploymentOrchestrator : IDeploymentOrchestrator
{
    private readonly IApplicationDiscoveryService _discoveryService;
    private readonly IIISDeploymentService _iisService;
    private readonly IDatabaseDeploymentService _databaseService;
    private readonly ICertificateService _certificateService;
    private readonly IHealthCheckService _healthCheckService;
    private readonly INotificationService _notificationService;
    private readonly IDeploymentLogger _logger;
    private readonly IRepository<Deployment> _deploymentRepo;
    private readonly IRepository<DeploymentProfile> _profileRepo;
    private readonly IAuditService _auditService;
    private readonly ICloudDeploymentService _cloudService;

    public DeploymentOrchestrator(
        IApplicationDiscoveryService discoveryService,
        IIISDeploymentService iisService,
        IDatabaseDeploymentService databaseService,
        ICertificateService certificateService,
        IHealthCheckService healthCheckService,
        INotificationService notificationService,
        IDeploymentLogger logger,
        IRepository<Deployment> deploymentRepo,
        IRepository<DeploymentProfile> profileRepo,
        IAuditService auditService,
        ICloudDeploymentService cloudService)
    {
        _discoveryService = discoveryService;
        _iisService = iisService;
        _databaseService = databaseService;
        _certificateService = certificateService;
        _healthCheckService = healthCheckService;
        _notificationService = notificationService;
        _logger = logger;
        _deploymentRepo = deploymentRepo;
        _profileRepo = profileRepo;
        _auditService = auditService;
        _cloudService = cloudService;
    }

    public async Task<DeploymentResult> DeployAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        Deployment deployment = null!;

        try
        {
            // Create deployment record
            deployment = await CreateDeploymentRecordAsync(request);
            deployment.Status = DeploymentStatus.InProgress;
            deployment.StartedAt = DateTime.UtcNow;
            await _deploymentRepo.UpdateAsync(deployment);

            await _logger.LogAsync(deployment.Id, LogLevel.Information, 
                "Deployment started", null);

            // Send start notification
            if (request.SendNotifications)
            {
                // Get notification config and send
            }

            // Detect cloud environment if needed
            if (request.Target == DeploymentTarget.AzureVM || 
                request.Target == DeploymentTarget.AWSEC2)
            {
                var cloudInfo = await _cloudService.DetectCloudEnvironmentAsync();
                deployment.CloudConfig = new CloudConfiguration
                {
                    Provider = cloudInfo.Provider,
                    PublicIpAddress = cloudInfo.PublicIp
                };
            }

            // Discover application
            await _logger.LogAsync(deployment.Id, LogLevel.Information, 
                "Discovering application...", null);
            
            var appDiscovery = await _discoveryService.DiscoverAsync(request.ApplicationPath);
            deployment.ApplicationType = appDiscovery.DetectedType;
            await _deploymentRepo.UpdateAsync(deployment);

            deployment.Status = DeploymentStatus.ValidationFailed;
            
            // Validate IIS installation
            if (!await _iisService.IsIISInstalledAsync())
            {
                await _logger.LogAsync(deployment.Id, LogLevel.Warning, 
                    "IIS not installed. Installing...", null);
                
                var progress = new Progress<ProgressInfo>(info =>
                {
                    _logger.LogAsync(deployment.Id, info.Level, info.Message, null).Wait();
                });
                
                await _iisService.InstallIISAsync(progress);
            }

            // Database deployment
            DatabaseDeploymentResult? dbResult = null;
            if (deployment.DatabaseConfig != null)
            {
                deployment.Status = DeploymentStatus.DatabaseDeploying;
                await _deploymentRepo.UpdateAsync(deployment);

                dbResult = await DeployDatabaseAsync(deployment, cancellationToken);
            }

            // SSL Certificate
            CertificateResult? certResult = null;
            if (deployment.SslConfig != null)
            {
                deployment.Status = DeploymentStatus.ConfiguringSSL;
                await _deploymentRepo.UpdateAsync(deployment);

                certResult = await ConfigureSSLAsync(deployment, cancellationToken);
                deployment.SslConfig.Thumbprint = certResult.Thumbprint;
                deployment.SslConfig.ExpiryDate = certResult.ExpiryDate;
            }

            // Deploy application to IIS
            deployment.Status = DeploymentStatus.AppDeploying;
            await _deploymentRepo.UpdateAsync(deployment);

            await DeployToIISAsync(deployment, cancellationToken);

            // Health checks
            HealthCheckSummary? healthSummary = null;
            if (request.RunHealthChecks)
            {
                deployment.Status = DeploymentStatus.RunningHealthChecks;
                await _deploymentRepo.UpdateAsync(deployment);

                healthSummary = await _healthCheckService.RunAllChecksAsync(deployment);
            }

            // Success!
            deployment.Status = DeploymentStatus.Success;
            deployment.CompletedAt = DateTime.UtcNow;
            deployment.DurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;
            deployment.RollbackCommand = $"iiscribe rollback --id {deployment.Id}";
            await _deploymentRepo.UpdateAsync(deployment);

            await _logger.LogAsync(deployment.Id, LogLevel.Information, 
                "Deployment completed successfully!", null);

            await _auditService.LogEventAsync(
                AuditEventType.DeploymentCompleted,
                deployment.CreatedBy,
                "Deploy",
                deployment.Id.ToString(),
                new Dictionary<string, object>
                {
                    ["Url"] = deployment.TargetUrl,
                    ["Duration"] = deployment.DurationSeconds
                });

            return new DeploymentResult
            {
                Success = true,
                DeploymentId = deployment.Id,
                Url = deployment.TargetUrl,
                DurationSeconds = deployment.DurationSeconds,
                Status = deployment.Status,
                DatabaseResult = dbResult,
                CertificateResult = certResult,
                HealthCheckSummary = healthSummary,
                RollbackCommand = deployment.RollbackCommand
            };
        }
        catch (Exception ex)
        {
            if (deployment != null)
            {
                deployment.Status = DeploymentStatus.Failed;
                deployment.ErrorMessage = ex.Message;
                deployment.CompletedAt = DateTime.UtcNow;
                deployment.DurationSeconds = (int)stopwatch.Elapsed.TotalSeconds;
                await _deploymentRepo.UpdateAsync(deployment);

                await _logger.LogAsync(deployment.Id, LogLevel.Error, 
                    "Deployment failed", ex);

                await _auditService.LogEventAsync(
                    AuditEventType.DeploymentFailed,
                    deployment.CreatedBy,
                    "Deploy",
                    deployment.Id.ToString(),
                    new Dictionary<string, object>
                    {
                        ["Error"] = ex.Message
                    });

                // Auto-rollback if configured
                if (deployment.DatabaseConfig?.AutoRollbackOnFailure == true)
                {
                    await RollbackAsync(deployment.Id, cancellationToken);
                }
            }

            return new DeploymentResult
            {
                Success = false,
                DeploymentId = deployment?.Id ?? Guid.Empty,
                Status = DeploymentStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<DeploymentResult> RollbackAsync(
        Guid deploymentId,
        CancellationToken cancellationToken = default)
    {
        var deployment = await _deploymentRepo.GetByIdAsync(deploymentId);
        if (deployment == null)
            throw new InvalidOperationException($"Deployment not found: {deploymentId}");

        try
        {
            await _logger.LogAsync(deploymentId, LogLevel.Warning, 
                "Starting rollback...", null);

            deployment.Status = DeploymentStatus.RolledBack;

            // Rollback database if backup exists
            if (deployment.DatabaseConfig?.BackupPath != null)
            {
                await _databaseService.RestoreDatabaseAsync(
                    deployment.DatabaseConfig, 
                    deployment.DatabaseConfig.BackupPath);
            }

            // Stop and remove IIS site
            if (deployment.SiteName != null)
            {
                await _iisService.RemoveSiteAsync(deployment.SiteName);
            }

            await _deploymentRepo.UpdateAsync(deployment);

            await _logger.LogAsync(deploymentId, LogLevel.Information, 
                "Rollback completed", null);

            await _auditService.LogEventAsync(
                AuditEventType.DeploymentRolledBack,
                "System",
                "Rollback",
                deploymentId.ToString());

            return new DeploymentResult
            {
                Success = true,
                DeploymentId = deploymentId,
                Status = DeploymentStatus.RolledBack
            };
        }
        catch (Exception ex)
        {
            await _logger.LogAsync(deploymentId, LogLevel.Error, 
                "Rollback failed", ex);

            return new DeploymentResult
            {
                Success = false,
                DeploymentId = deploymentId,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<DeploymentStatus> GetStatusAsync(Guid deploymentId)
    {
        var deployment = await _deploymentRepo.GetByIdAsync(deploymentId);
        return deployment?.Status ?? DeploymentStatus.Failed;
    }

    public async Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync()
    {
        return await _deploymentRepo.FindAsync(d => 
            d.Status == DeploymentStatus.InProgress ||
            d.Status == DeploymentStatus.Pending);
    }

    private async Task<Deployment> CreateDeploymentRecordAsync(DeploymentRequest request)
    {
        DeploymentProfile? profile = null;
        if (!string.IsNullOrEmpty(request.ProfileId))
        {
            profile = await _profileRepo.GetByIdAsync(Guid.Parse(request.ProfileId));
        }

        var domainName = request.DomainName ?? profile?.DomainPattern ?? "myapp.local";
        var httpPort = request.HttpPort ?? profile?.HttpPort ?? 80;
        var httpsPort = request.HttpsPort ?? profile?.HttpsPort ?? 443;

        var deployment = new Deployment
        {
            Name = $"Deployment_{DateTime.Now:yyyyMMdd_HHmmss}",
            ApplicationPath = request.ApplicationPath,
            Target = request.Target ?? profile?.Target ?? DeploymentTarget.LocalIIS,
            Environment = request.Environment ?? profile?.Environment ?? DeploymentEnvironment.Development,
            Strategy = request.Strategy ?? profile?.Strategy ?? DeploymentStrategy.StopAndDeploy,
            DomainName = domainName,
            HttpPort = httpPort,
            HttpsPort = httpsPort,
            TargetUrl = $"https://{domainName}:{httpsPort}",
            SiteName = domainName.Replace(".", "_"),
            AppPoolName = $"AppPool_{domainName.Replace(".", "_")}",
            RuntimeVersion = profile?.RuntimeVersion ?? AppPoolRuntimeVersion.NoManagedCode,
            PipelineMode = profile?.PipelineMode ?? PipelineMode.Integrated,
            DatabaseConfig = request.DatabaseConfig ?? profile?.DatabaseTemplate,
            SslConfig = request.SslConfig ?? profile?.SslTemplate,
            CloudConfig = request.CloudConfig ?? profile?.CloudTemplate,
            ProfileId = profile?.Id,
            Status = DeploymentStatus.Pending,
            CreatedBy = "System" // Should come from auth context
        };

        return await _deploymentRepo.AddAsync(deployment);
    }

    private async Task<DatabaseDeploymentResult> DeployDatabaseAsync(
        Deployment deployment,
        CancellationToken cancellationToken)
    {
        var config = deployment.DatabaseConfig!;

        await _logger.LogAsync(deployment.Id, LogLevel.Information, 
            "Starting database deployment...", null);

        // Backup existing database
        BackupResult? backupResult = null;
        if (config.BackupBeforeDeployment && await _databaseService.DatabaseExistsAsync(config))
        {
            var backupPath = Path.Combine(
                config.BackupPath ?? Path.GetTempPath(),
                $"{config.DatabaseName}_backup_{DateTime.Now:yyyyMMddHHmmss}.bak");

            backupResult = await _databaseService.BackupDatabaseAsync(config, backupPath);
            config.BackupPath = backupResult.BackupPath;
        }

        // Create database if needed
        if (!await _databaseService.DatabaseExistsAsync(config))
        {
            var progress = new Progress<ProgressInfo>(info =>
            {
                _logger.LogAsync(deployment.Id, info.Level, info.Message, null).Wait();
            });

            await _databaseService.CreateDatabaseAsync(config, progress);
        }

        // Run scripts
        int scriptsExecuted = 0;
        if (config.ScriptPaths.Any())
        {
            var progress = new Progress<ProgressInfo>(info =>
            {
                _logger.LogAsync(deployment.Id, info.Level, info.Message, null).Wait();
            });

            await _databaseService.RunScriptsAsync(config, config.ScriptPaths, progress);
            scriptsExecuted = config.ScriptPaths.Count;
        }

        // Run migrations
        if (!string.IsNullOrEmpty(config.MigrationsFolder))
        {
            await _databaseService.RunMigrationsAsync(config, config.MigrationsFolder);
        }

        // Update connection string in app config
        var configFile = Directory.GetFiles(deployment.ApplicationPath, "*.config", SearchOption.TopDirectoryOnly)
            .FirstOrDefault() ?? 
            Directory.GetFiles(deployment.ApplicationPath, "appsettings.json", SearchOption.TopDirectoryOnly)
            .FirstOrDefault();

        if (configFile != null)
        {
            await _databaseService.UpdateConnectionStringAsync(
                configFile, 
                config.ConnectionString ?? BuildConnectionString(config));
        }

        return new DatabaseDeploymentResult
        {
            Success = true,
            DatabaseName = config.DatabaseName ?? "Unknown",
            ServerName = config.ServerName ?? "(localdb)",
            ScriptsExecuted = scriptsExecuted,
            BackupPath = backupResult?.BackupPath
        };
    }

    private async Task<CertificateResult> ConfigureSSLAsync(
        Deployment deployment,
        CancellationToken cancellationToken)
    {
        var config = deployment.SslConfig!;

        await _logger.LogAsync(deployment.Id, LogLevel.Information, 
            $"Configuring SSL certificate ({config.CertificateType})...", null);

        CertificateResult result = config.CertificateType switch
        {
            CertificateType.SelfSigned => 
                await _certificateService.GenerateSelfSignedAsync(deployment.DomainName, config.ValidityDays),
            
            CertificateType.LetsEncrypt => 
                await _certificateService.RequestLetsEncryptAsync(deployment.DomainName, config.LetsEncryptEmail!),
            
            CertificateType.CustomCertificate => 
                await _certificateService.InstallCertificateAsync(config.CertificatePath!, config.CertificatePassword!),
            
            CertificateType.AzureKeyVault => 
                await _certificateService.GetFromKeyVaultAsync(config.KeyVaultUrl!, config.CertificateName!),
            
            _ => throw new NotSupportedException($"Certificate type not supported: {config.CertificateType}")
        };

        return result;
    }

    private async Task DeployToIISAsync(Deployment deployment, CancellationToken cancellationToken)
    {
        await _logger.LogAsync(deployment.Id, LogLevel.Information, 
            "Deploying to IIS...", null);

        // Create app pool
        var appPoolConfig = new AppPoolConfiguration
        {
            Name = deployment.AppPoolName!,
            RuntimeVersion = deployment.RuntimeVersion,
            PipelineMode = deployment.PipelineMode,
            AlwaysRunning = deployment.Environment == DeploymentEnvironment.Production
        };

        await _iisService.CreateApplicationPoolAsync(appPoolConfig);

        // Create website
        var websiteConfig = new WebsiteConfiguration
        {
            Name = deployment.SiteName!,
            PhysicalPath = Path.Combine(@"C:\inetpub\wwwroot", deployment.SiteName!),
            AppPoolName = deployment.AppPoolName!,
            DomainName = deployment.DomainName,
            HttpPort = deployment.HttpPort,
            HttpsPort = deployment.HttpsPort,
            EnableHttps = deployment.SslConfig != null,
            CertificateThumbprint = deployment.SslConfig?.Thumbprint
        };

        await _iisService.CreateWebsiteAsync(websiteConfig);

        // Deploy files
        var deployConfig = new ApplicationDeploymentConfig
        {
            SourcePath = deployment.ApplicationPath,
            DestinationPath = websiteConfig.PhysicalPath,
            SiteName = deployment.SiteName!,
            StopSiteBeforeDeployment = true,
            BackupExisting = true
        };

        var progress = new Progress<ProgressInfo>(info =>
        {
            _logger.LogAsync(deployment.Id, info.Level, info.Message, null).Wait();
        });

        await _iisService.DeployApplicationAsync(deployConfig, progress);
    }

    private string BuildConnectionString(DatabaseConfiguration config)
    {
        return config.Provider switch
        {
            DatabaseProvider.SqlServerLocalDb => 
                $"Server=(localdb)\\mssqllocaldb;Database={config.DatabaseName};Trusted_Connection=true;",
            
            DatabaseProvider.SqlServer => 
                $"Server={config.ServerName};Database={config.DatabaseName};Integrated Security=true;",
            
            _ => throw new NotSupportedException($"Provider not supported: {config.Provider}")
        };
    }
}
