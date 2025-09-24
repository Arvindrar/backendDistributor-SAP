// File: Controllers/WarehouseController.cs
using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class WarehouseController : ControllerBase
{
    private readonly WarehouseService _warehouseService;

    public WarehouseController(WarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    // GET: api/Warehouse
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Warehouse>>> GetWarehouses()
    {
        var warehouses = await _warehouseService.GetAllAsync();
        return Ok(warehouses);
    }

    // POST: api/Warehouse
    [HttpPost]
    public async Task<ActionResult<Warehouse>> PostWarehouse(Warehouse warehouse)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var newWarehouse = await _warehouseService.AddAsync(warehouse);
        return CreatedAtAction(nameof(GetWarehouses), new { id = newWarehouse.Id }, newWarehouse);
    }

    // DELETE: api/Warehouse/5  or  api/Warehouse/L101
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteWarehouse(string id)
    {
        try
        {
            await _warehouseService.DeleteAsync(id);
            return NoContent(); // Success
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex)
        {
            // Log the exception
            return StatusCode(500, "An internal error occurred.");
        }
    }
}