using IIScribe.Core.Entities;
using IIScribe.Core.Enums;
using IIScribe.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IIScribe.Web.Controllers;

/// <summary>
/// Deployment profiles and templates management (Story 2.2: Client Profile Management)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ProfilesController : ControllerBase
{
    private readonly IProfileService _profileService;
    private readonly IRepository<DeploymentProfile> _profileRepo;
    private readonly ILogger<ProfilesController> _logger;

    public ProfilesController(
        IProfileService profileService,
        IRepository<DeploymentProfile> profileRepo,
        ILogger<ProfilesController> logger)
    {
        _profileService = profileService;
        _profileRepo = profileRepo;
        _logger = logger;
    }

    /// <summary>
    /// Create a new deployment profile
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DeploymentProfile), 201)]
    public async Task<ActionResult<DeploymentProfile>> CreateProfile(
        [FromBody] DeploymentProfile profile)
    {
        try
        {
            var created = await _profileService.CreateProfileAsync(profile);
            return CreatedAtAction(nameof(GetProfile), new { id = created.Id }, created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating profile");
            return BadRequest(new ProblemDetails
            {
                Title = "Profile Creation Failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get a profile by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(DeploymentProfile), 200)]
    public async Task<ActionResult<DeploymentProfile>> GetProfile(Guid id)
    {
        var profile = await _profileService.GetProfileAsync(id);
        if (profile == null)
            return NotFound();

        return Ok(profile);
    }

    /// <summary>
    /// List profiles by category (Story 2.2: Profile categories)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<DeploymentProfile>), 200)]
    public async Task<ActionResult<IEnumerable<DeploymentProfile>>> ListProfiles(
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] string? clientName = null,
        [FromQuery] string? teamName = null)
    {
        IEnumerable<DeploymentProfile> profiles;

        if (!string.IsNullOrEmpty(search))
        {
            profiles = await _profileService.SearchProfilesAsync(search);
        }
        else if (!string.IsNullOrEmpty(category))
        {
            profiles = await _profileService.GetProfilesByCategoryAsync(category);
        }
        else
        {
            profiles = await _profileRepo.GetAllAsync();
        }

        // Additional filtering
        if (!string.IsNullOrEmpty(clientName))
            profiles = profiles.Where(p => p.ClientName == clientName);

        if (!string.IsNullOrEmpty(teamName))
            profiles = profiles.Where(p => p.TeamName == teamName);

        return Ok(profiles.OrderByDescending(p => p.LastUsedAt ?? p.CreatedAt));
    }

    /// <summary>
    /// Update a profile
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromBody] DeploymentProfile profile)
    {
        var existing = await _profileRepo.GetByIdAsync(id);
        if (existing == null)
            return NotFound();

        profile.Id = id;
        profile.CreatedAt = existing.CreatedAt;
        profile.CreatedBy = existing.CreatedBy;
        profile.UpdatedAt = DateTime.UtcNow;

        await _profileRepo.UpdateAsync(profile);
        return NoContent();
    }

    /// <summary>
    /// Delete a profile
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> DeleteProfile(Guid id)
    {
        await _profileRepo.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>
    /// Export profile as JSON (Story 3.1: Team Standardization)
    /// </summary>
    [HttpGet("{id}/export")]
    [ProducesResponseType(typeof(string), 200)]
    public async Task<ActionResult<string>> ExportProfile(Guid id)
    {
        try
        {
            var json = await _profileService.ExportProfileAsync(id);
            return Ok(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting profile {Id}", id);
            return BadRequest(new ProblemDetails
            {
                Title = "Export Failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Import profile from JSON (Story 3.1: Team Standardization)
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(DeploymentProfile), 201)]
    public async Task<ActionResult<DeploymentProfile>> ImportProfile([FromBody] string json)
    {
        try
        {
            var profile = await _profileService.ImportProfileAsync(json);
            return CreatedAtAction(nameof(GetProfile), new { id = profile.Id }, profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing profile");
            return BadRequest(new ProblemDetails
            {
                Title = "Import Failed",
                Detail = ex.Message
            });
        }
    }

    /// <summary>
    /// Get profile templates (Story 1.3: Template-Based Learning)
    /// </summary>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IEnumerable<DeploymentProfile>), 200)]
    public async Task<ActionResult<IEnumerable<DeploymentProfile>>> GetTemplates()
    {
        var templates = await _profileRepo.FindAsync(p => p.IsTemplate);
        return Ok(templates.OrderBy(t => t.Name));
    }
}
