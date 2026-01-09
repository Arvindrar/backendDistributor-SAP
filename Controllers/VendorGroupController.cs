// REPLACE the entire content of Controllers/VendorGroupController.cs
using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes; // Required for JsonNode

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VendorGroupController : ControllerBase
    {
        private readonly VendorGroupService _vendorGroupService;
        private readonly ILogger<VendorGroupController> _logger;

        public VendorGroupController(VendorGroupService vendorGroupService, ILogger<VendorGroupController> logger)
        {
            _vendorGroupService = vendorGroupService;
            _logger = logger;
        }

        // GET: api/VendorGroup
        [HttpGet]
        public async Task<IActionResult> GetVendorGroups()
        {
            try
            {
                var result = await _vendorGroupService.GetAllAsync();
                return Content(result, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get vendor groups.");
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }

        // POST: api/VendorGroup
        [HttpPost]
        public async Task<IActionResult> CreateVendorGroup([FromBody] JsonNode groupData)
        {
            try
            {
                var result = await _vendorGroupService.AddAsync(groupData);
                return CreatedAtAction(nameof(GetVendorGroups), new { }, result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create vendor group.");
                // Check for duplicate error from SAP
                if (ex.Message.Contains("already exists"))
                {
                    return Conflict(new { message = "This vendor group name already exists." });
                }
                return StatusCode(500, new { message = $"An error occurred: {ex.Message}" });
            }
        }

        // DELETE: api/VendorGroup/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVendorGroup(int id)
        {
            try
            {
                await _vendorGroupService.DeleteAsync(id);
                return NoContent(); // Success
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete vendor group with ID {id}.", id);
                // Check for 'in use' error from SAP
                if (ex.Message.Contains("Linked"))
                {
                    return Conflict(new { message = "Cannot delete group. It is already in use by one or more vendors." });
                }
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }
    }
}