// FILE: backendDistributor/Controllers/UOMsController.cs
using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UOMsController : ControllerBase
    {
        private readonly UomService _uomService;
        private readonly ILogger<UOMsController> _logger;

        public UOMsController(UomService uomService, ILogger<UOMsController> logger)
        {
            _uomService = uomService;
            _logger = logger;
        }

        // GET: api/UOMs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UOM>>> GetUOMs()
        {
            try
            {
                var uoms = await _uomService.GetAllAsync();
                return Ok(uoms);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all UOMs.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // POST: api/UOMs
        [HttpPost]
        public async Task<ActionResult<UOM>> PostUOM(UOM uom)
        {
            try
            {
                var newUom = await _uomService.AddAsync(uom);
                // Return the complete object created, including its new ID
                return CreatedAtAction(nameof(GetUOMs), new { id = newUom.Id }, newUom);
            }
            catch (ArgumentException ex) // For empty names
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex) // For duplicate names in SQL
            {
                return Conflict(ex.Message);
            }
            catch (Exception ex) // For SAP errors or other issues
            {
                _logger.LogError(ex, "An error occurred while creating a new UOM.");
                return StatusCode(500, ex.Message);
            }
        }

        // DELETE: api/UOMs/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUOM(int id)
        {
            try
            {
                await _uomService.DeleteAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while deleting UOM with ID {id}.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}