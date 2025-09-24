// Controllers/ShippingTypeController.cs
using backendDistributor.Models;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class ShippingTypeController : ControllerBase
{
    private readonly ShippingTypeService _shippingTypeService;
    private readonly ILogger<ShippingTypeController> _logger;

    public ShippingTypeController(ShippingTypeService shippingTypeService, ILogger<ShippingTypeController> logger)
    {
        _shippingTypeService = shippingTypeService;
        _logger = logger;
    }

    // GET: api/ShippingType
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShippingType>>> GetShippingTypes()
    {
        try
        {
            var shippingTypes = await _shippingTypeService.GetAllAsync();
            return Ok(shippingTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting shipping types.");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteShippingType(int id)
    {
        try
        {
            await _shippingTypeService.DeleteAsync(id);
            return NoContent(); // Success (204 No Content)
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Attempted to delete a non-existent shipping type with ID {Id}.", id);
            return NotFound(new { message = ex.Message }); // 404 Not Found
        }
        catch (HttpRequestException ex) // Catches errors from SAP
        {
            _logger.LogError(ex, "An error from SAP occurred while deleting shipping type {Id}.", id);
            return StatusCode(500, $"SAP Service Layer error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting shipping type {Id}.", id);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
    // POST: api/ShippingType
    [HttpPost]
    public async Task<ActionResult<ShippingType>> PostShippingType([FromBody] ShippingType shippingType)
    {
        if (shippingType == null || string.IsNullOrWhiteSpace(shippingType.Name))
        {
            return BadRequest(new { message = "Shipping type name cannot be empty." });
        }

        try
        {
            var newShippingType = await _shippingTypeService.AddAsync(shippingType);
            // Return 201 Created with the new object
            return CreatedAtAction(nameof(GetShippingTypes), new { id = newShippingType.Id }, newShippingType);
        }
        catch (InvalidOperationException ex) // Catches duplicate name error from SQL
        {
            _logger.LogWarning(ex, "Attempted to create a duplicate shipping type.");
            return Conflict(new { message = ex.Message }); // Return 409 Conflict
        }
        catch (HttpRequestException ex) // Catches errors from SAP
        {
            _logger.LogError(ex, "An error from SAP occurred while creating a shipping type.");
            // Try to return a more specific message if SAP provides one
            if (ex.Message.Contains("already exists"))
            {
                return Conflict(new { message = "A Shipping Type with this name already exists in SAP." });
            }
            return StatusCode(500, $"SAP Service Layer error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating a shipping type.");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}