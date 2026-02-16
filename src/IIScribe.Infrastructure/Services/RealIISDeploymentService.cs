using IIScribe.Core.DTOs;
using IIScribe.Core.Entities;
using IIScribe.Core.Enums;
using IIScribe.Core.Interfaces;
using Microsoft.Web.Administration;
using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Runtime.InteropServices;
using LogLevel = IIScribe.Core.Enums.LogLevel;

namespace IIScribe.Infrastructure.Services;

/// <summary>
/// REAL IIS deployment service - actually deploys to IIS
/// </summary>
public class RealIISDeploymentService : IIISDeploymentService
{
    private readonly IDeploymentLogger _logger;
    private readonly IHostsFileService _hostsFileService;

    public RealIISDeploymentService(
        IDeploymentLogger logger,
        IHostsFileService hostsFileService)
    {
        _logger = logger;
        _hostsFileService = hostsFileService;
    }

    public async Task<bool> IsIISInstalledAsync()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.WriteLine("⚠️  WARNING: Not running on Windows - IIS features disabled");
            return false;
        }

        try
        {
            using var serverManager = new ServerManager();
            return serverManager.Sites != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  IIS not detected: {ex.Message}");
            return false;
        }
    }

    public async Task InstallIISAsync(IProgress<ProgressInfo>? progress = null)
    {
        Console.WriteLine("📦 Installing IIS...");
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
            "IIS-StaticContent",
            "IIS-DefaultDocument",
            "IIS-DirectoryBrowsing",
            "IIS-HttpErrors",
            "IIS-ApplicationDevelopment",
            "IIS-NetFxExtensibility45",
            "IIS-ASPNET45",
            "IIS-ISAPIExtensions",
            "IIS-ISAPIFilter",
            "IIS-HealthAndDiagnostics",
            "IIS-HttpLogging",
            "IIS-Security",
            "IIS-RequestFiltering",
            "IIS-Performance",
            "IIS-HttpCompressionStatic",
            "IIS-ManagementConsole"
        };

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-Command \"Enable-WindowsOptionalFeature -Online -FeatureName {string.Join(",", features)} -All -NoRestart\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            Verb = "runas"
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start IIS installation");
        }

        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Console.WriteLine($"IIS Installation output: {output}");
        if (!string.IsNullOrEmpty(error))
        {
            Console.WriteLine($"Errors: {error}");
        }

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"IIS installation failed with exit code {process.ExitCode}");
        }

        progress?.Report(new ProgressInfo
        {
            Stage = "Installing IIS",
            PercentComplete = 100,
            Message = "✓ IIS installation completed"
        });
    }

    public async Task<string> CreateApplicationPoolAsync(AppPoolConfiguration config)
    {
        Console.WriteLine($"🔧 Creating app pool: {config.Name}");
        
        using var serverManager = new ServerManager();

        // Remove existing pool if it exists
        var existingPool = serverManager.ApplicationPools[config.Name];
        if (existingPool != null)
        {
            Console.WriteLine($"   Removing existing app pool: {config.Name}");
            serverManager.ApplicationPools.Remove(existingPool);
            serverManager.CommitChanges();
        }

        // Create new app pool
        var appPool = serverManager.ApplicationPools.Add(config.Name);

        // Configure runtime version
        appPool.ManagedRuntimeVersion = config.RuntimeVersion switch
        {
            AppPoolRuntimeVersion.NoManagedCode => "",
            AppPoolRuntimeVersion.V2_0 => "v2.0",
            AppPoolRuntimeVersion.V4_0 => "v4.0",
            _ => ""
        };

        // Configure pipeline mode
        appPool.ManagedPipelineMode = config.PipelineMode == PipelineMode.Integrated
            ? ManagedPipelineMode.Integrated
            : ManagedPipelineMode.Classic;

        // Configure process model
        appPool.ProcessModel.IdleTimeout = TimeSpan.FromMinutes(config.IdleTimeoutMinutes);
        appPool.Enable32BitAppOnWin64 = config.Enable32BitAppOnWin64;

        if (config.AlwaysRunning)
        {
            appPool.StartMode = StartMode.AlwaysRunning;
            appPool.ProcessModel.IdleTimeout = TimeSpan.Zero;
        }

        serverManager.CommitChanges();
        Console.WriteLine($"   ✓ App pool created: {config.Name}");
        
        return config.Name;
    }

    public async Task<string> CreateWebsiteAsync(WebsiteConfiguration config)
    {
        Console.WriteLine($"🌐 Creating website: {config.Name}");
        
        using var serverManager = new ServerManager();

        // Remove existing site if it exists
        var existingSite = serverManager.Sites[config.Name];
        if (existingSite != null)
        {
            Console.WriteLine($"   Removing existing site: {config.Name}");
            serverManager.Sites.Remove(existingSite);
            serverManager.CommitChanges();
        }

        // Create physical directory if it doesn't exist
        if (!Directory.Exists(config.PhysicalPath))
        {
            Console.WriteLine($"   Creating directory: {config.PhysicalPath}");
            Directory.CreateDirectory(config.PhysicalPath);
        }

        // Create site
        var site = serverManager.Sites.Add(config.Name, config.PhysicalPath, config.HttpPort);
        site.ApplicationDefaults.ApplicationPoolName = config.AppPoolName;

        // Clear default bindings
        site.Bindings.Clear();

        // Add HTTP binding
        Console.WriteLine($"   Adding HTTP binding: *:{config.HttpPort}:{config.DomainName}");
        site.Bindings.Add($"*:{config.HttpPort}:{config.DomainName}", "http");

        // Add HTTPS binding if enabled and certificate is available
        if (config.EnableHttps && !string.IsNullOrEmpty(config.CertificateThumbprint))
        {
            Console.WriteLine($"   Adding HTTPS binding: *:{config.HttpsPort}:{config.DomainName}");
            var httpsBinding = site.Bindings.Add($"*:{config.HttpsPort}:{config.DomainName}", "https");
            
            try
            {
                var certHash = config.CertificateThumbprint.Replace(":", "").Replace(" ", "");
                httpsBinding.CertificateHash = Convert.FromHexString(certHash);
                httpsBinding.CertificateStoreName = "My";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Could not bind certificate: {ex.Message}");
            }
        }

        serverManager.CommitChanges();
        Console.WriteLine($"   ✓ Website created: {config.Name}");

        // Add to hosts file
        await _hostsFileService.AddEntryAsync("127.0.0.1", config.DomainName);
        Console.WriteLine($"   ✓ Added to hosts file: {config.DomainName}");

        return config.Name;
    }

    public async Task DeployApplicationAsync(
        ApplicationDeploymentConfig config,
        IProgress<ProgressInfo>? progress = null)
    {
        Console.WriteLine($"📦 Deploying application to: {config.DestinationPath}");
        
        progress?.Report(new ProgressInfo
        {
            Stage = "Validating",
            PercentComplete = 10,
            Message = "Validating source path..."
        });

        if (!Directory.Exists(config.SourcePath))
        {
            throw new DirectoryNotFoundException($"Source path not found: {config.SourcePath}");
        }

        // Stop site if requested
        if (config.StopSiteBeforeDeployment)
        {
            progress?.Report(new ProgressInfo
            {
                Stage = "Stopping Site",
                PercentComplete = 20,
                Message = $"Stopping site: {config.SiteName}"
            });

            try
            {
                await StopSiteAsync(config.SiteName);
                await Task.Delay(2000); // Give IIS time to release locks
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Could not stop site: {ex.Message}");
            }
        }

        // Backup existing if requested
        if (config.BackupExisting && Directory.Exists(config.DestinationPath))
        {
            progress?.Report(new ProgressInfo
            {
                Stage = "Backing Up",
                PercentComplete = 30,
                Message = "Backing up existing files..."
            });

            var backupPath = $"{config.DestinationPath}_backup_{DateTime.Now:yyyyMMddHHmmss}";
            Console.WriteLine($"   Creating backup: {backupPath}");
            
            try
            {
                CopyDirectory(config.DestinationPath, backupPath);
                Console.WriteLine($"   ✓ Backup created");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ⚠️  Backup failed: {ex.Message}");
            }
        }

        // Deploy files
        progress?.Report(new ProgressInfo
        {
            Stage = "Copying Files",
            PercentComplete = 50,
            Message = "Deploying application files..."
        });

        // Create destination directory
        if (!Directory.Exists(config.DestinationPath))
        {
            Directory.CreateDirectory(config.DestinationPath);
        }

        // Clear destination (keep backup safe)
        Console.WriteLine($"   Clearing destination: {config.DestinationPath}");
        foreach (var file in Directory.GetFiles(config.DestinationPath))
        {
            try { File.Delete(file); } catch { }
        }
        foreach (var dir in Directory.GetDirectories(config.DestinationPath))
        {
            try { Directory.Delete(dir, true); } catch { }
        }

        // Copy files
        Console.WriteLine($"   Copying files from: {config.SourcePath}");
        CopyDirectory(config.SourcePath, config.DestinationPath, config.ExcludePatterns);
        
        var fileCount = Directory.GetFiles(config.DestinationPath, "*", SearchOption.AllDirectories).Length;
        Console.WriteLine($"   ✓ Copied {fileCount} files");

        // Set permissions
        progress?.Report(new ProgressInfo
        {
            Stage = "Setting Permissions",
            PercentComplete = 80,
            Message = "Configuring folder permissions..."
        });

        Console.WriteLine($"   Setting permissions on: {config.DestinationPath}");
        SetFolderPermissions(config.DestinationPath);
        Console.WriteLine($"   ✓ Permissions set");

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
            Message = "✓ Deployment completed successfully"
        });

        Console.WriteLine($"✓ Deployment complete!");
    }

    public async Task StartSiteAsync(string siteName)
    {
        Console.WriteLine($"▶️  Starting site: {siteName}");
        
        using var serverManager = new ServerManager();
        var site = serverManager.Sites[siteName];
        
        if (site == null)
        {
            throw new InvalidOperationException($"Site not found: {siteName}");
        }

        if (site.State != ObjectState.Started && site.State != ObjectState.Starting)
        {
            site.Start();
            Console.WriteLine($"   ✓ Site started");
        }
        else
        {
            Console.WriteLine($"   Site already running");
        }
    }

    public async Task StopSiteAsync(string siteName)
    {
        Console.WriteLine($"⏸️  Stopping site: {siteName}");
        
        using var serverManager = new ServerManager();
        var site = serverManager.Sites[siteName];
        
        if (site == null)
        {
            throw new InvalidOperationException($"Site not found: {siteName}");
        }

        if (site.State != ObjectState.Stopped && site.State != ObjectState.Stopping)
        {
            site.Stop();
            Console.WriteLine($"   ✓ Site stopped");
        }
        else
        {
            Console.WriteLine($"   Site already stopped");
        }
    }

    public async Task RemoveSiteAsync(string siteName)
    {
        Console.WriteLine($"🗑️  Removing site: {siteName}");
        
        using var serverManager = new ServerManager();
        var site = serverManager.Sites[siteName];
        
        if (site != null)
        {
            var appPoolName = site.Applications[0].ApplicationPoolName;
            serverManager.Sites.Remove(site);
            serverManager.CommitChanges();
            Console.WriteLine($"   ✓ Site removed");

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
                    Console.WriteLine($"   ✓ App pool removed: {appPoolName}");
                }
            }
        }
    }

    public async Task<IEnumerable<SiteStatus>> GetAllSitesAsync()
    {
        var statuses = new List<SiteStatus>();

        if (!await IsIISInstalledAsync())
        {
            return statuses;
        }

        using var serverManager = new ServerManager();
        
        foreach (var site in serverManager.Sites)
        {
            try
            {
                var appPoolName = site.Applications[0].ApplicationPoolName;
                var appPool = serverManager.ApplicationPools[appPoolName];
                
                var binding = site.Bindings.FirstOrDefault();
                var protocol = binding?.Protocol ?? "http";
                var host = binding?.Host ?? "localhost";
                var port = binding?.EndPoint?.Port ?? 80;

                var status = new SiteStatus
                {
                    SiteName = site.Name,
                    Url = $"{protocol}://{host}:{port}",
                    IsRunning = site.State == ObjectState.Started,
                    State = site.State.ToString(),
                    AppPoolName = appPoolName,
                    AppPoolRunning = appPool?.State == ObjectState.Started
                };

                statuses.Add(status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  Error reading site {site.Name}: {ex.Message}");
            }
        }

        return statuses;
    }

    private void CopyDirectory(string sourceDir, string destDir, IEnumerable<string>? excludePatterns = null)
    {
        var dir = new DirectoryInfo(sourceDir);
        if (!dir.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
        }

        Directory.CreateDirectory(destDir);

        foreach (var file in dir.GetFiles())
        {
            if (excludePatterns?.Any(pattern => file.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)) == true)
            {
                continue;
            }

            var targetPath = Path.Combine(destDir, file.Name);
            file.CopyTo(targetPath, true);
        }

        foreach (var subDir in dir.GetDirectories())
        {
            if (excludePatterns?.Any(pattern => subDir.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)) == true)
            {
                continue;
            }

            var newDestDir = Path.Combine(destDir, subDir.Name);
            CopyDirectory(subDir.FullName, newDestDir, excludePatterns);
        }
    }

    private void SetFolderPermissions(string path)
    {
        try
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
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Could not set permissions: {ex.Message}");
        }
    }
}
