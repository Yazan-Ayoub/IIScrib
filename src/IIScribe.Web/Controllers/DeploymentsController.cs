using IIScribe.Core.DTOs;
using IIScribe.Core.Entities;
using IIScribe.Core.Enums;
using IIScribe.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using LogLevel = IIScribe.Core.Enums.LogLevel;

namespace IIScribe.Web.Controllers;

/// <summary>
/// Deployment orchestration endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DeploymentsController : ControllerBase
{
    private readonly IDeploymentOrchestrator _orchestrator;
    private readonly IRepository<Deployment> _deploymentRepo;
    private readonly ILogger<DeploymentsController> _logger;

    public DeploymentsController(
        IDeploymentOrchestrator orchestrator,
        IRepository<Deployment> deploymentRepo,
        ILogger<DeploymentsController> logger)
    {
        _orchestrator = orchestrator;
        _deploymentRepo = deploymentRepo;
        _logger = logger;
    }

    /// <summary>
    /// Deploy an application (Story 1.1: One-Click Local Deployment)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeploymentResult), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<ActionResult<DeploymentResult>> Deploy(
        [FromBody] DeploymentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Deployment requested for: {Path}", request.ApplicationPath);

            var result = await _orchestrator.DeployAsync(request, cancellationToken);

            if (result.Success)
            {
                return Ok(result);
            }

            return BadRequest(new ProblemDetails
            {
                Title = "Deployment Failed",
                Detail = result.ErrorMessage,
                Status = 400
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment error");
            return BadRequest(new ProblemDetails
            {
                Title = "Deployment Error",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Get deployment status (Story 1.2: Visual Progress & Feedback)
    /// </summary>
    [HttpGet("{id}/status")]
    [ProducesResponseType(typeof(DeploymentStatusInfo), 200)]
    public async Task<ActionResult<DeploymentStatusInfo>> GetStatus(Guid id)
    {
        var deployment = await _deploymentRepo.GetByIdAsync(id);
        if (deployment == null)
            return NotFound();

        var logs = deployment.Logs.OrderByDescending(l => l.CreatedAt).Take(10);

        return Ok(new DeploymentStatusInfo
        {
            DeploymentId = deployment.Id,
            Status = deployment.Status,
            PercentComplete = CalculateProgress(deployment.Status),
            CurrentStage = deployment.Status.ToString(),
            StartedAt = deployment.StartedAt,
            RecentLogs = logs.Select(l => l.Message).ToArray()
        });
    }

    /// <summary>
    /// Get deployment details
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(Deployment), 200)]
    public async Task<ActionResult<Deployment>> GetDeployment(Guid id)
    {
        var deployment = await _deploymentRepo.GetByIdAsync(id);
        if (deployment == null)
            return NotFound();

        return Ok(deployment);
    }

    /// <summary>
    /// List all deployments (Story 9.1: Executive Dashboard)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Deployment>), 200)]
    public async Task<ActionResult<IEnumerable<Deployment>>> ListDeployments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] DeploymentStatus? status = null,
        [FromQuery] DeploymentEnvironment? environment = null)
    {
        var deployments = await _deploymentRepo.GetAllAsync();

        // Filter
        if (status.HasValue)
            deployments = deployments.Where(d => d.Status == status.Value);

        if (environment.HasValue)
            deployments = deployments.Where(d => d.Environment == environment.Value);

        // Sort by most recent
        deployments = deployments.OrderByDescending(d => d.CreatedAt);

        // Paginate
        var paginated = deployments
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        return Ok(paginated);
    }

    /// <summary>
    /// Rollback a deployment (Story 5.3: Auto-Rollback on Failure)
    /// </summary>
    [HttpPost("{id}/rollback")]
    [ProducesResponseType(typeof(DeploymentResult), 200)]
    public async Task<ActionResult<DeploymentResult>> Rollback(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _orchestrator.RollbackAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback error for deployment: {Id}", id);
            return BadRequest(new ProblemDetails
            {
                Title = "Rollback Failed",
                Detail = ex.Message,
                Status = 500
            });
        }
    }

    /// <summary>
    /// Get deployment logs
    /// </summary>
    [HttpGet("{id}/logs")]
    [ProducesResponseType(typeof(IEnumerable<DeploymentLog>), 200)]
    public async Task<ActionResult<IEnumerable<DeploymentLog>>> GetLogs(
        Guid id,
        [FromQuery] LogLevel? level = null)
    {
        var deployment = await _deploymentRepo.GetByIdAsync(id);
        if (deployment == null)
            return NotFound();

        var logs = deployment.Logs.AsEnumerable();

        if (level.HasValue)
            logs = logs.Where(l => l.Level == level.Value);

        return Ok(logs.OrderBy(l => l.CreatedAt));
    }

    /// <summary>
    /// Get dashboard summary (Story 9.1: Executive Dashboard)
    /// </summary>
    [HttpGet("dashboard/summary")]
    [ProducesResponseType(typeof(DashboardSummary), 200)]
    public async Task<ActionResult<DashboardSummary>> GetDashboardSummary()
    {
        var allDeployments = await _deploymentRepo.GetAllAsync();
        var last30Days = allDeployments.Where(d => 
            d.CreatedAt >= DateTime.UtcNow.AddDays(-30));

        var successCount = last30Days.Count(d => d.Status == DeploymentStatus.Success);
        var totalCount = last30Days.Count();

        return Ok(new DashboardSummary
        {
            TotalDeployments = allDeployments.Count(),
            ActiveSites = 0, // Would come from IIS service
            SuccessRate = totalCount > 0 ? (double)successCount / totalCount * 100 : 0,
            DeploymentsLast30Days = totalCount,
            CertificatesExpiringSoon = 0, // Would come from certificate service
            RecentDeployments = allDeployments
                .OrderByDescending(d => d.CreatedAt)
                .Take(5)
        });
    }

    private int CalculateProgress(DeploymentStatus status)
    {
        return status switch
        {
            DeploymentStatus.Pending => 0,
            DeploymentStatus.InProgress => 20,
            DeploymentStatus.ValidationFailed => 15,
            DeploymentStatus.BackupInProgress => 30,
            DeploymentStatus.DatabaseDeploying => 50,
            DeploymentStatus.AppDeploying => 70,
            DeploymentStatus.ConfiguringSSL => 80,
            DeploymentStatus.RunningHealthChecks => 90,
            DeploymentStatus.Success => 100,
            DeploymentStatus.Failed => 100,
            DeploymentStatus.RolledBack => 100,
            _ => 0
        };
    }
}
