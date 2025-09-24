// Replace the entire contents of Controllers/TaxDeclarationsController.cs
using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TaxDeclarationsController : ControllerBase
    {
        private readonly TaxService _taxService;
        private readonly ILogger<TaxDeclarationsController> _logger;

        public TaxDeclarationsController(TaxService taxService, ILogger<TaxDeclarationsController> logger)
        {
            _taxService = taxService;
            _logger = logger;
        }

        // GET: api/TaxDeclarations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TaxDeclaration>>> GetTaxDeclarations()
        {
            try
            {
                var taxes = await _taxService.GetAllAsync();
                return Ok(taxes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting tax declarations.");
                return StatusCode(500, ex.Message);
            }
        }

        // POST: api/TaxDeclarations
        [HttpPost]
        public async Task<ActionResult<TaxDeclaration>> PostTaxDeclaration(TaxDeclaration taxDeclaration)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var newTax = await _taxService.AddAsync(taxDeclaration);
                return CreatedAtAction(nameof(GetTaxDeclarations), new { id = newTax.Id }, newTax);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tax declaration.");
                return StatusCode(500, ex.Message);
            }
        }
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTaxDeclaration(int id, TaxDeclaration taxDeclaration)
        {
            // A simple check to ensure the ID in the URL matches the body
            if (id != taxDeclaration.Id)
            {
                return BadRequest("ID mismatch in request.");
            }

            try
            {
                await _taxService.UpdateAsync(id, taxDeclaration);
                return NoContent(); // Success
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating tax declaration with id {id}.");
                return StatusCode(500, ex.Message);
            }
        }
        // DELETE: api/TaxDeclarations/5
        [HttpDelete("{taxCode}")] // The parameter is now a string named taxCode
        public async Task<IActionResult> DeleteTaxDeclaration(string taxCode)
        {
            if (string.IsNullOrEmpty(taxCode))
            {
                return BadRequest("Tax code cannot be empty.");
            }

            try
            {
                await _taxService.DeleteByCodeAsync(taxCode); // We will create this new method next
                return NoContent(); // Success
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting tax with code {taxCode}.");
                return StatusCode(500, ex.Message);
            }
        }

    }
}