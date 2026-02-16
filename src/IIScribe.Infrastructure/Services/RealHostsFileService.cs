using IIScribe.Core.Interfaces;
using System.Runtime.InteropServices;

namespace IIScribe.Infrastructure.Services;

/// <summary>
/// REAL hosts file service - actually modifies C:\Windows\System32\drivers\etc\hosts
/// </summary>
public class RealHostsFileService : IHostsFileService
{
    private readonly string _hostsFilePath;
    private const string IISCRIBE_MARKER = "# IIScribe";

    public RealHostsFileService()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            _hostsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"drivers\etc\hosts");
        }
        else
        {
            _hostsFilePath = "/etc/hosts";
        }
    }

    public async Task AddEntryAsync(string ipAddress, string hostname)
    {
        try
        {
            // Check if entry already exists
            if (await EntryExistsAsync(hostname))
            {
                Console.WriteLine($"   Hosts entry already exists: {hostname}");
                return;
            }

            // Read existing content
            var lines = new List<string>();
            if (File.Exists(_hostsFilePath))
            {
                lines = (await File.ReadAllLinesAsync(_hostsFilePath)).ToList();
            }

            // Add new entry
            var newEntry = $"{ipAddress,-20} {hostname,-30} {IISCRIBE_MARKER}";
            lines.Add(newEntry);

            // Write back
            await File.WriteAllLinesAsync(_hostsFilePath, lines);
            Console.WriteLine($"   ✓ Added to hosts file: {ipAddress} → {hostname}");
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine($"   ⚠️  Access denied to hosts file. Run as Administrator!");
            Console.WriteLine($"   ⚠️  You'll need to manually add: {ipAddress} {hostname}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Could not update hosts file: {ex.Message}");
        }
    }

    public async Task RemoveEntryAsync(string hostname)
    {
        try
        {
            if (!File.Exists(_hostsFilePath))
            {
                return;
            }

            var lines = await File.ReadAllLinesAsync(_hostsFilePath);
            var newLines = lines.Where(line =>
                !line.Contains(hostname, StringComparison.OrdinalIgnoreCase) ||
                !line.Contains(IISCRIBE_MARKER)).ToList();

            if (newLines.Count != lines.Length)
            {
                await File.WriteAllLinesAsync(_hostsFilePath, newLines);
                Console.WriteLine($"   ✓ Removed from hosts file: {hostname}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Could not remove from hosts file: {ex.Message}");
        }
    }

    public async Task<bool> EntryExistsAsync(string hostname)
    {
        try
        {
            if (!File.Exists(_hostsFilePath))
            {
                return false;
            }

            var lines = await File.ReadAllLinesAsync(_hostsFilePath);
            return lines.Any(line =>
                line.Contains(hostname, StringComparison.OrdinalIgnoreCase) &&
                !line.TrimStart().StartsWith("#"));
        }
        catch
        {
            return false;
        }
    }

    public async Task BackupAsync()
    {
        try
        {
            if (!File.Exists(_hostsFilePath))
            {
                return;
            }

            var backupPath = $"{_hostsFilePath}.backup_{DateTime.Now:yyyyMMddHHmmss}";
            File.Copy(_hostsFilePath, backupPath, true);
            Console.WriteLine($"   ✓ Hosts file backed up to: {backupPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Could not backup hosts file: {ex.Message}");
        }
    }

    public async Task RestoreAsync()
    {
        try
        {
            var backupFiles = Directory.GetFiles(
                Path.GetDirectoryName(_hostsFilePath)!,
                "hosts.backup_*");

            if (backupFiles.Length == 0)
            {
                Console.WriteLine("   No backup files found");
                return;
            }

            var latestBackup = backupFiles.OrderByDescending(f => f).First();
            File.Copy(latestBackup, _hostsFilePath, true);
            Console.WriteLine($"   ✓ Hosts file restored from: {latestBackup}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Could not restore hosts file: {ex.Message}");
        }
    }
}
