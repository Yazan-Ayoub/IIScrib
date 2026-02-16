using IIScribe.Core.DTOs;
using IIScribe.Core.Entities;
using IIScribe.Core.Enums;
using IIScribe.Core.Interfaces;
using Microsoft.Web.Administration;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using LogLevel = IIScribe.Core.Enums.LogLevel;

namespace IIScribe.Infrastructure.Services;

/// <summary>
/// IIS deployment and management service
/// </summary>
public class IISDeploymentService : IIISDeploymentService
{
    private readonly IDeploymentLogger _logger;
    private readonly IHostsFileService _hostsFileService;

    public IISDeploymentService(
        IDeploymentLogger logger,
        IHostsFileService hostsFileService)
    {
        _logger = logger;
        _hostsFileService = hostsFileService;
    }

    public async Task<bool> IsIISInstalledAsync()
    {
        try
        {
            using var serverManager = new ServerManager();
            return serverManager.Sites != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task InstallIISAsync(IProgress<ProgressInfo>? progress = null)
    {
        progress?.Report(new ProgressInfo
        {
            Stage = "Installing IIS",
            PercentComplete = 0,
            Message = "Starting IIS installation..."
        });

        var features = new[]
        {
            "IIS-WebServerRole",
            "IIS-WebServer",
            "IIS-CommonHttpFeatures",
            "IIS-HttpErrors",
            "IIS-HttpRedirect",
            "IIS-ApplicationDevelopment",
            "IIS-NetFxExtensibility45",
            "IIS-HealthAndDiagnostics",
            "IIS-HttpLogging",
            "IIS-LoggingLibraries",
            "IIS-RequestMonitor",
            "IIS-HttpTracing",
            "IIS-Security",
            "IIS-RequestFiltering",
            "IIS-Performance",
            "IIS-WebServerManagementTools",
            "IIS-IIS6ManagementCompatibility",
            "IIS-Metabase",
            "IIS-ManagementConsole",
            "IIS-BasicAuthentication",
            "IIS-WindowsAuthentication",
            "IIS-StaticContent",
            "IIS-DefaultDocument",
            "IIS-DirectoryBrowsing",
            "IIS-ASPNET45",
            "IIS-ISAPIExtensions",
            "IIS-ISAPIFilter",
            "IIS-HttpCompressionStatic",
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = "dism.exe",
            Arguments = $"/Online /Enable-Feature /All {string.Join(" ", features.Select(f => $"/FeatureName:{f}"))}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = "runas"
        };

        using var process = Process.Start(startInfo);
        if (process == null)
            throw new InvalidOperationException("Failed to start IIS installation process");

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"IIS installation failed: {error}");
        }

        progress?.Report(new ProgressInfo
        {
            Stage = "Installing IIS",
            PercentComplete = 100,
            Message = "IIS installation completed successfully"
        });
    }

    public async Task<string> CreateApplicationPoolAsync(AppPoolConfiguration config)
    {
        using var serverManager = new ServerManager();

        // Check if app pool already exists
        var existingPool = serverManager.ApplicationPools[config.Name];
        if (existingPool != null)
        {
            serverManager.ApplicationPools.Remove(existingPool);
        }

        // Create new app pool
        var appPool = serverManager.ApplicationPools.Add(config.Name);

        // Configure runtime version
        appPool.ManagedRuntimeVersion = config.RuntimeVersion switch
        {
            AppPoolRuntimeVersion.NoManagedCode => "",
            AppPoolRuntimeVersion.V2_0 => "v2.0",
            AppPoolRuntimeVersion.V4_0 => "v4.0",
            _ => "v4.0"
        };

        // Configure pipeline mode
        appPool.ManagedPipelineMode = config.PipelineMode == PipelineMode.Integrated
            ? ManagedPipelineMode.Integrated
            : ManagedPipelineMode.Classic;

        // Configure process model
        appPool.ProcessModel.IdleTimeout = TimeSpan.FromMinutes(config.IdleTimeoutMinutes);
        appPool.Enable32BitAppOnWin64 = config.Enable32BitAppOnWin64;

        // Configure always running
        if (config.AlwaysRunning)
        {
            appPool.StartMode = StartMode.AlwaysRunning;
            appPool.ProcessModel.IdleTimeout = TimeSpan.Zero;
        }

        // Set identity
        if (!string.IsNullOrEmpty(config.Identity))
        {
            appPool.ProcessModel.IdentityType = ProcessModelIdentityType.SpecificUser;
            appPool.ProcessModel.UserName = config.Identity;
        }

        serverManager.CommitChanges();
        return config.Name;
    }

    public async Task<string> CreateWebsiteAsync(WebsiteConfiguration config)
    {
        using var serverManager = new ServerManager();

        // Remove existing site if it exists
        var existingSite = serverManager.Sites[config.Name];
        if (existingSite != null)
        {
            serverManager.Sites.Remove(existingSite);
        }

        // Create site
        var site = serverManager.Sites.Add(config.Name, config.PhysicalPath, config.HttpPort);
        site.ApplicationDefaults.ApplicationPoolName = config.AppPoolName;

        // Clear default bindings
        site.Bindings.Clear();

        // Add HTTP binding
        site.Bindings.Add($"*:{config.HttpPort}:{config.DomainName}", "http");

        // Add HTTPS binding if enabled
        if (config.EnableHttps && !string.IsNullOrEmpty(config.CertificateThumbprint))
        {
            var httpsBinding = site.Bindings.Add($"*:{config.HttpsPort}:{config.DomainName}", "https");
            httpsBinding.CertificateHash = Convert.FromHexString(config.CertificateThumbprint.Replace(":", ""));
            httpsBinding.CertificateStoreName = "My";
        }

        serverManager.CommitChanges();

        // Add to hosts file
        await _hostsFileService.AddEntryAsync("127.0.0.1", config.DomainName);

        return config.Name;
    }

    public async Task DeployApplicationAsync(
        ApplicationDeploymentConfig config,
        IProgress<ProgressInfo>? progress = null)
    {
        progress?.Report(new ProgressInfo
        {
            Stage = "Preparing Deployment",
            PercentComplete = 10,
            Message = "Validating source path..."
        });

        if (!Directory.Exists(config.SourcePath))
            throw new DirectoryNotFoundException($"Source path not found: {config.SourcePath}");

        // Stop site if requested
        if (config.StopSiteBeforeDeployment)
        {
            progress?.Report(new ProgressInfo
            {
                Stage = "Stopping Site",
                PercentComplete = 20,
                Message = $"Stopping site: {config.SiteName}"
            });

            await StopSiteAsync(config.SiteName);
            await Task.Delay(2000); // Give IIS time to release file locks
        }

        // Backup existing if requested
        if (config.BackupExisting && Directory.Exists(config.DestinationPath))
        {
            progress?.Report(new ProgressInfo
            {
                Stage = "Creating Backup",
                PercentComplete = 30,
                Message = "Backing up existing application..."
            });

            var backupPath = $"{config.DestinationPath}_backup_{DateTime.Now:yyyyMMddHHmmss}";
            CopyDirectory(config.DestinationPath, backupPath);
        }

        // Deploy files
        progress?.Report(new ProgressInfo
        {
            Stage = "Copying Files",
            PercentComplete = 50,
            Message = "Deploying application files..."
        });

        if (Directory.Exists(config.DestinationPath))
        {
            Directory.Delete(config.DestinationPath, true);
        }

        Directory.CreateDirectory(config.DestinationPath);
        CopyDirectory(config.SourcePath, config.DestinationPath, config.ExcludePatterns);

        // Set permissions
        progress?.Report(new ProgressInfo
        {
            Stage = "Setting Permissions",
            PercentComplete = 80,
            Message = "Configuring folder permissions..."
        });

        SetFolderPermissions(config.DestinationPath);

        // Start site
        progress?.Report(new ProgressInfo
        {
            Stage = "Starting Site",
            PercentComplete = 90,
            Message = $"Starting site: {config.SiteName}"
        });

        await StartSiteAsync(config.SiteName);

        progress?.Report(new ProgressInfo
        {
            Stage = "Complete",
            PercentComplete = 100,
            Message = "Deployment completed successfully"
        });
    }

    public async Task StartSiteAsync(string siteName)
    {
        using var serverManager = new ServerManager();
        var site = serverManager.Sites[siteName];
        if (site == null)
            throw new InvalidOperationException($"Site not found: {siteName}");

        if (site.State != ObjectState.Started)
        {
            site.Start();
        }
    }

    public async Task StopSiteAsync(string siteName)
    {
        using var serverManager = new ServerManager();
        var site = serverManager.Sites[siteName];
        if (site == null)
            throw new InvalidOperationException($"Site not found: {siteName}");

        if (site.State != ObjectState.Stopped)
        {
            site.Stop();
        }
    }

    public async Task RemoveSiteAsync(string siteName)
    {
        using var serverManager = new ServerManager();
        var site = serverManager.Sites[siteName];
        if (site != null)
        {
            var appPoolName = site.Applications[0].ApplicationPoolName;
            serverManager.Sites.Remove(site);
            serverManager.CommitChanges();

            // Remove app pool if not used by other sites
            var appPool = serverManager.ApplicationPools[appPoolName];
            if (appPool != null)
            {
                var isUsed = serverManager.Sites.Any(s =>
                    s.Applications.Any(a => a.ApplicationPoolName == appPoolName));

                if (!isUsed)
                {
                    serverManager.ApplicationPools.Remove(appPool);
                    serverManager.CommitChanges();
                }
            }
        }
    }

    public async Task<IEnumerable<SiteStatus>> GetAllSitesAsync()
    {
        using var serverManager = new ServerManager();
        var statuses = new List<SiteStatus>();

        foreach (var site in serverManager.Sites)
        {
            var appPoolName = site.Applications[0].ApplicationPoolName;
            var appPool = serverManager.ApplicationPools[appPoolName];

            var status = new SiteStatus
            {
                SiteName = site.Name,
                Url = site.Bindings.FirstOrDefault()?.Protocol == "https"
                    ? $"https://{site.Bindings.First().Host}"
                    : $"http://{site.Bindings.First().Host}",
                IsRunning = site.State == ObjectState.Started,
                State = site.State.ToString(),
                AppPoolName = appPoolName,
                AppPoolRunning = appPool?.State == ObjectState.Started
            };

            statuses.Add(status);
        }

        return statuses;
    }

    private void CopyDirectory(string sourceDir, string destDir, IEnumerable<string>? excludePatterns = null)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

        Directory.CreateDirectory(destDir);

        foreach (var file in dir.GetFiles())
        {
            if (excludePatterns?.Any(pattern => file.Name.Contains(pattern)) == true)
                continue;

            var targetPath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetPath, true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            if (excludePatterns?.Any(pattern => subDir.Name.Contains(pattern)) == true)
                continue;

            var newDestDir = Path.Combine(destDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestDir, excludePatterns);
        }
    }

    private void SetFolderPermissions(string path)
    {
        var dirInfo = new DirectoryInfo(path);
        var dirSecurity = dirInfo.GetAccessControl();

        // Add IIS_IUSRS group with modify permissions
        var iisUsersIdentity = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);
        var accessRule = new FileSystemAccessRule(
            iisUsersIdentity,
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow);

        dirSecurity.AddAccessRule(accessRule);
        dirInfo.SetAccessControl(dirSecurity);
    }
}
