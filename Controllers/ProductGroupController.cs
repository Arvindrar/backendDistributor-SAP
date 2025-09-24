// FILE: backendDistributor/Controllers/ProductGroupController.cs

using Microsoft.AspNetCore.Mvc;
using backendDistributor.Models;
using backendDistributor.Services;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductGroupController : ControllerBase
    {
        private readonly ProductGroupService _productGroupService;
        private readonly ILogger<ProductGroupController> _logger;

        public ProductGroupController(ProductGroupService productGroupService, ILogger<ProductGroupController> logger)
        {
            _productGroupService = productGroupService;
            _logger = logger;
        }

        // GET: api/ProductGroup
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ProductGroup>>> GetProductGroups()
        {
            try
            {
                var groups = await _productGroupService.GetAllAsync();
                return Ok(groups);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all Product Groups.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        // POST: api/ProductGroup
        [HttpPost]
        public async Task<ActionResult<ProductGroup>> PostProductGroup(ProductGroup productGroup)
        {
            try
            {
                var newGroup = await _productGroupService.AddAsync(productGroup);
                return CreatedAtAction(nameof(GetProductGroups), new { id = newGroup.Id }, newGroup);
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
                _logger.LogError(ex, "An error occurred while creating a new Product Group.");
                return StatusCode(500, ex.Message);
            }
        }

        // DELETE: api/ProductGroup/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProductGroup(int id)
        {
            try
            {
                await _productGroupService.DeleteAsync(id);
                return NoContent();
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"An error occurred while deleting Product Group with ID {id}.");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}