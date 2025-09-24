// Replace the code in your Controllers/UOMGroupsController.cs

using backendDistributor.Models;
using backendDistributor.Services; // <-- Add this using statement
using Microsoft.AspNetCore.Mvc;

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UOMGroupsController : ControllerBase
    {
        private readonly UomGroupService _uomGroupService;
        private readonly ILogger<UOMGroupsController> _logger;

        // The controller now only depends on the service, not the DbContext
        public UOMGroupsController(UomGroupService uomGroupService, ILogger<UOMGroupsController> logger)
        {
            _uomGroupService = uomGroupService;
            _logger = logger;
        }

        // GET: api/UOMGroups
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UOMGroup>>> GetUOMGroups()
        {
            try
            {
                var groups = await _uomGroupService.GetAllAsync();
                return Ok(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all UOM Groups.");
                return StatusCode(500, "An internal server error occurred.");
            }
        }

        // POST: api/UOMGroups
        [HttpPost]
        public async Task<ActionResult<UOMGroup>> PostUOMGroup(UOMGroup uomGroup)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var createdGroup = await _uomGroupService.AddAsync(uomGroup);
                // Return a 201 Created response with the location of the new resource
                return CreatedAtAction(nameof(GetUOMGroups), new { id = createdGroup.Id }, createdGroup);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a UOM Group.");
                // Return the specific error message from SAP or the database if possible
                return StatusCode(500, ex.Message);
            }
        }

        // DELETE: api/UOMGroups/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUOMGroup(int id)
        {
            try
            {
                await _uomGroupService.DeleteAsync(id);
                return NoContent(); // Success, 204 No Content
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning(ex.Message);
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while deleting UOM Group with ID {Id}.", id);
                return StatusCode(500, ex.Message);
            }
        }
    }
}