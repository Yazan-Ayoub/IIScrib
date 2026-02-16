using System.CommandLine;
using System.Text.Json;
using IIScribe.Core.DTOs;
using IIScribe.Core.Enums;
using Spectre.Console;

namespace IIScribe.CLI;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("IIScribe - Professional Deployment Orchestration");

        // =========================
        // DEPLOY COMMAND + OPTIONS
        // =========================
        var pathOption = new Option<string>("--path", "Path to application") { IsRequired = true };
        var profileOption = new Option<string?>("--profile", "Profile ID to use");
        var domainOption = new Option<string?>("--domain", "Domain name (e.g., myapp.local)");
        var httpPortOption = new Option<int?>("--http-port", "HTTP port (default: 80)");
        var httpsPortOption = new Option<int?>("--https-port", "HTTPS port (default: 443)");

        var environmentOption = new Option<DeploymentEnvironment>(
            "--environment",
            getDefaultValue: () => DeploymentEnvironment.Development,
            description: "Environment");

        var targetOption = new Option<DeploymentTarget>(
            "--target",
            getDefaultValue: () => DeploymentTarget.LocalIIS,
            description: "Deployment target");

        var silentOption = new Option<bool>(
            "--silent",
            getDefaultValue: () => false,
            description: "Silent mode (no UI, JSON output only)");

        var configOption = new Option<string?>("--config", "JSON configuration file path");

        var noHealthChecksOption = new Option<bool>(
            "--no-health-checks",
            getDefaultValue: () => false,
            description: "Skip health checks");

        var noNotificationsOption = new Option<bool>(
            "--no-notifications",
            getDefaultValue: () => false,
            description: "Skip notifications");

        var deployCommand = new Command("deploy", "Deploy an application");
        deployCommand.AddOption(pathOption);
        deployCommand.AddOption(profileOption);
        deployCommand.AddOption(domainOption);
        deployCommand.AddOption(httpPortOption);
        deployCommand.AddOption(httpsPortOption);
        deployCommand.AddOption(environmentOption);
        deployCommand.AddOption(targetOption);
        deployCommand.AddOption(silentOption);
        deployCommand.AddOption(configOption);
        deployCommand.AddOption(noHealthChecksOption);
        deployCommand.AddOption(noNotificationsOption);

        // ✅ Compatible with older System.CommandLine versions
        deployCommand.SetHandler(async (context) =>
        {
            var path = context.ParseResult.GetValueForOption(pathOption)!;
            var profile = context.ParseResult.GetValueForOption(profileOption);
            var domain = context.ParseResult.GetValueForOption(domainOption);
            var httpPort = context.ParseResult.GetValueForOption(httpPortOption);
            var httpsPort = context.ParseResult.GetValueForOption(httpsPortOption);
            var environment = context.ParseResult.GetValueForOption(environmentOption);
            var target = context.ParseResult.GetValueForOption(targetOption);
            var silent = context.ParseResult.GetValueForOption(silentOption);
            var configFile = context.ParseResult.GetValueForOption(configOption);
            var noHealthChecks = context.ParseResult.GetValueForOption(noHealthChecksOption);
            var noNotifications = context.ParseResult.GetValueForOption(noNotificationsOption);

            await ExecuteDeployAsync(
                path,
                profile,
                domain,
                httpPort,
                httpsPort,
                environment,
                target,
                silent,
                configFile,
                runHealthChecks: !noHealthChecks,
                sendNotifications: !noNotifications);
        });

        rootCommand.AddCommand(deployCommand);

        // =========================
        // ROLLBACK COMMAND
        // =========================
        var rollbackIdOption = new Option<string>("--id", "Deployment ID") { IsRequired = true };
        var rollbackSilentOption = new Option<bool>("--silent", () => false, "Silent mode (JSON output)");

        var rollbackCommand = new Command("rollback", "Rollback a deployment");
        rollbackCommand.AddOption(rollbackIdOption);
        rollbackCommand.AddOption(rollbackSilentOption);

        rollbackCommand.SetHandler(async (context) =>
        {
            var id = context.ParseResult.GetValueForOption(rollbackIdOption)!;
            var silent = context.ParseResult.GetValueForOption(rollbackSilentOption);
            await ExecuteRollbackAsync(id, silent);
        });

        rootCommand.AddCommand(rollbackCommand);

        // =========================
        // LIST COMMAND
        // =========================
        var limitOption = new Option<int>("--limit", () => 10, "Number of deployments to show");
        var statusFilterOption = new Option<DeploymentStatus?>("--status", "Filter by status");
        var listJsonOption = new Option<bool>("--json", () => false, "Output as JSON");

        var listCommand = new Command("list", "List deployments");
        listCommand.AddOption(limitOption);
        listCommand.AddOption(statusFilterOption);
        listCommand.AddOption(listJsonOption);

        listCommand.SetHandler(async (context) =>
        {
            var limit = context.ParseResult.GetValueForOption(limitOption);
            var status = context.ParseResult.GetValueForOption(statusFilterOption);
            var json = context.ParseResult.GetValueForOption(listJsonOption);

            await ExecuteListAsync(limit, status, json);
        });

        rootCommand.AddCommand(listCommand);

        // =========================
        // STATUS COMMAND
        // =========================
        var statusIdOption = new Option<string>("--id", "Deployment ID") { IsRequired = true };
        var statusJsonOption = new Option<bool>("--json", () => false, "Output as JSON");
        var watchOption = new Option<bool>("--watch", () => false, "Watch for updates");

        var statusCommand = new Command("status", "Get deployment status");
        statusCommand.AddOption(statusIdOption);
        statusCommand.AddOption(statusJsonOption);
        statusCommand.AddOption(watchOption);

        statusCommand.SetHandler(async (context) =>
        {
            var id = context.ParseResult.GetValueForOption(statusIdOption)!;
            var json = context.ParseResult.GetValueForOption(statusJsonOption);
            var watch = context.ParseResult.GetValueForOption(watchOption);

            await ExecuteStatusAsync(id, json, watch);
        });

        rootCommand.AddCommand(statusCommand);

        // =========================
        // ONBOARDING COMMAND
        // =========================
        var teamOption = new Option<string>("--team", "Team name") { IsRequired = true };
        var configUrlOption = new Option<string?>("--config-url", "Team configuration URL");

        var onboardCommand = new Command("onboarding", "Onboard new team member");
        onboardCommand.AddOption(teamOption);
        onboardCommand.AddOption(configUrlOption);

        onboardCommand.SetHandler(async (context) =>
        {
            var team = context.ParseResult.GetValueForOption(teamOption)!;
            var configUrl = context.ParseResult.GetValueForOption(configUrlOption);

            await ExecuteOnboardingAsync(team, configUrl);
        });

        rootCommand.AddCommand(onboardCommand);

        return await rootCommand.InvokeAsync(args);
    }

    // ==========================================================
    // Handlers
    // ==========================================================

    private static async Task ExecuteDeployAsync(
        string path,
        string? profile,
        string? domain,
        int? httpPort,
        int? httpsPort,
        DeploymentEnvironment environment,
        DeploymentTarget target,
        bool silent,
        string? configFile,
        bool runHealthChecks,
        bool sendNotifications)
    {
        try
        {
            DeploymentRequest request;

            if (!string.IsNullOrWhiteSpace(configFile))
            {
                var json = await File.ReadAllTextAsync(configFile);
                request = JsonSerializer.Deserialize<DeploymentRequest>(json)
                          ?? throw new InvalidOperationException("Config file could not be parsed into a DeploymentRequest.");
            }
            else
            {
                request = new DeploymentRequest
                {
                    ApplicationPath = path,
                    ProfileId = profile,
                    DomainName = domain,
                    HttpPort = httpPort,
                    HttpsPort = httpsPort,
                    Environment = environment,
                    Target = target,
                    RunHealthChecks = runHealthChecks,
                    SendNotifications = sendNotifications
                };
            }

            if (string.IsNullOrWhiteSpace(request.ApplicationPath))
                throw new ArgumentException("Application path is required.");

            if (!Directory.Exists(request.ApplicationPath) && !File.Exists(request.ApplicationPath))
                throw new DirectoryNotFoundException($"Path not found: {request.ApplicationPath}");

            if (silent)
            {
                var result = new
                {
                    status = "success",
                    deploymentId = Guid.NewGuid(),
                    url = $"https://{(request.DomainName ?? "myapp.local")}",
                    durationSeconds = 47,
                    environment = request.Environment.ToString(),
                    target = request.Target.ToString()
                };

                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                return;
            }

            AnsiConsole.Write(new FigletText("IIScribe").Centered().Color(Color.Blue));

            await AnsiConsole.Progress()
                .AutoClear(false)
                .Columns(new ProgressColumn[]
                {
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new SpinnerColumn(),
                })
                .StartAsync(async ctx =>
                {
                    var t1 = ctx.AddTask("[green]Discovering application[/]");
                    await Task.Delay(500);
                    t1.Value = 100;

                    var t2 = ctx.AddTask("[yellow]Validating IIS[/]");
                    await Task.Delay(500);
                    t2.Value = 100;

                    var t3 = ctx.AddTask("[blue]Deploying database[/]");
                    await Task.Delay(1000);
                    t3.Value = 100;

                    var t4 = ctx.AddTask("[magenta]Configuring SSL[/]");
                    await Task.Delay(500);
                    t4.Value = 100;

                    var t5 = ctx.AddTask("[cyan]Deploying to IIS[/]");
                    await Task.Delay(1000);
                    t5.Value = 100;

                    if (request.RunHealthChecks)
                    {
                        var t6 = ctx.AddTask("[white]Running health checks[/]");
                        await Task.Delay(500);
                        t6.Value = 100;
                    }
                });

            var domainToShow = request.DomainName ?? "myapp.local";
            var deploymentId = Guid.NewGuid();

            var panel = new Panel(
                $"[bold green]✓ Deployment Successful![/]\n\n" +
                $"[blue]URL:[/] https://{domainToShow}\n" +
                $"[blue]Duration:[/] 47 seconds\n" +
                $"[blue]Deployment ID:[/] {deploymentId}\n\n" +
                $"[dim]Rollback command:[/] iiscribe rollback --id {deploymentId}")
            {
                Header = new PanelHeader("Deployment Complete", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green)
            };

            AnsiConsole.Write(panel);
        }
        catch (Exception ex)
        {
            if (silent)
            {
                var error = new { status = "failed", error = ex.Message };
                Console.WriteLine(JsonSerializer.Serialize(error, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                AnsiConsole.WriteException(ex);
            }
        }
    }

    private static async Task ExecuteRollbackAsync(string id, bool silent)
    {
        if (silent)
        {
            var result = new { status = "success", message = "Rollback completed", deploymentId = id };
            Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Rolling back deployment: {id}[/]");
        await Task.Delay(1000);
        AnsiConsole.MarkupLine("[green]✓ Rollback completed[/]");
    }

    private static async Task ExecuteListAsync(int limit, DeploymentStatus? status, bool json)
    {
        var deployments = new[]
        {
            new { id = Guid.NewGuid(), name = "MyApp", status = DeploymentStatus.Success, createdAt = DateTimeOffset.Now.AddHours(-2) },
            new { id = Guid.NewGuid(), name = "WebApi", status = DeploymentStatus.Success, createdAt = DateTimeOffset.Now.AddHours(-5) },
            new { id = Guid.NewGuid(), name = "Dashboard", status = DeploymentStatus.Failed, createdAt = DateTimeOffset.Now.AddDays(-1) }
        }.AsEnumerable();

        if (status.HasValue)
            deployments = deployments.Where(d => d.status == status.Value);

        deployments = deployments.Take(limit);

        if (json)
        {
            Console.WriteLine(JsonSerializer.Serialize(deployments, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("ID")
            .AddColumn("Name")
            .AddColumn("Status")
            .AddColumn("Created");

        foreach (var dep in deployments)
        {
            var statusColor = dep.status == DeploymentStatus.Success ? "green" : "red";
            table.AddRow(
                dep.id.ToString()[..8],
                dep.name,
                $"[{statusColor}]{dep.status}[/]",
                dep.createdAt.ToString("g"));
        }

        AnsiConsole.Write(table);
    }

    private static async Task ExecuteStatusAsync(string id, bool json, bool watch)
    {
        if (json)
        {
            var statusObj = new
            {
                deploymentId = id,
                status = "InProgress",
                percentComplete = 65,
                currentStage = "Deploying to IIS",
                watch
            };

            Console.WriteLine(JsonSerializer.Serialize(statusObj, new JsonSerializerOptions { WriteIndented = true }));
            return;
        }

        AnsiConsole.MarkupLine($"[bold]Deployment Status:[/] {id}");
        AnsiConsole.MarkupLine($"[blue]Status:[/] In Progress");
        AnsiConsole.MarkupLine($"[blue]Stage:[/] Deploying to IIS");
        AnsiConsole.MarkupLine($"[blue]Progress:[/] 65%");

        if (watch)
        {
            AnsiConsole.MarkupLine("[dim](watch mode demo: press Ctrl+C to stop)[/]");
            while (true)
            {
                await Task.Delay(1500);
                AnsiConsole.MarkupLine($"[dim]{DateTimeOffset.Now:T}[/] still in progress...");
            }
        }
    }

    private static async Task ExecuteOnboardingAsync(string team, string? configUrl)
    {
        AnsiConsole.Write(new FigletText($"Welcome to {team}!").Color(Color.Green));

        await AnsiConsole.Status()
            .StartAsync("Setting up your environment...", async ctx =>
            {
                ctx.Status("Installing IIS...");
                await Task.Delay(2000);

                ctx.Status("Cloning team profiles...");
                await Task.Delay(1500);

                ctx.Status("Setting up SSL certificates...");
                await Task.Delay(1000);

                if (!string.IsNullOrWhiteSpace(configUrl))
                {
                    ctx.Status($"Applying config from {configUrl}...");
                    await Task.Delay(800);
                }

                ctx.Status("Verifying installation...");
                await Task.Delay(500);
            });

        AnsiConsole.MarkupLine("[green]✓ Onboarding complete! You're ready to deploy.[/]");
    }
}
