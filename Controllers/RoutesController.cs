// PASTE THIS ENTIRE CORRECTED CODE INTO YOUR RoutesController.cs FILE

using Microsoft.AspNetCore.Mvc;
using backendDistributor.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

// FIX #1: Standardize the API route to be singular: "/api/Route"
[Route("api/Route")]
[ApiController]
public class RoutesController : ControllerBase
{
    private readonly RouteService _routeService;
    private readonly ILogger<RoutesController> _logger;

    public RoutesController(RouteService routeService, ILogger<RoutesController> logger)
    {
        _routeService = routeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<RouteNode>>> GetRoutes()
    {
        try
        {
            var routes = await _routeService.GetAllAsync();

            // FIX #2: Wrap the response in a "value" object to be consistent
            // with the Customer API and what the frontend expects.
            return Ok(new { value = routes });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting routes.");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // --- NO CHANGES NEEDED BELOW, BUT KEEP THE CODE ---

    [HttpPost]
    public async Task<ActionResult<RouteNode>> PostRoute([FromBody] RouteNode route)
    {
        // ... (existing code is fine)
        if (string.IsNullOrWhiteSpace(route.Name))
        {
            return BadRequest(new { message = "Route name cannot be empty." });
        }
        try
        {
            var newRoute = await _routeService.AddAsync(route);
            return CreatedAtAction(nameof(GetRoutes), new { id = newRoute.Id }, newRoute);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating a route.");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpPatch("{id}")]
    public async Task<IActionResult> UpdateRoute(int id, [FromBody] RouteNode route)
    {
        // ... (existing code is fine)
        if (string.IsNullOrWhiteSpace(route.Name))
        {
            return BadRequest(new { message = "Route name cannot be empty." });
        }
        try
        {
            await _routeService.UpdateAsync(id, route);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating route {Id}.", id);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRoute(int id)
    {
        // ... (existing code is fine)
        try
        {
            await _routeService.DeleteAsync(id);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting route {Id}.", id);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}