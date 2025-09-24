// Controllers/CustomerGroupController.cs

using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

[Route("api/[controller]")]
[ApiController]
public class CustomerGroupController : ControllerBase
{
    private readonly CustomerGroupService _customerGroupService;
    private readonly ILogger<CustomerGroupController> _logger;

    public CustomerGroupController(CustomerGroupService customerGroupService, ILogger<CustomerGroupController> logger)
    {
        _customerGroupService = customerGroupService;
        _logger = logger;
    }

    // GET: api/CustomerGroup
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomerGroup>>> GetCustomerGroups()
    {
        try
        {
            var groups = await _customerGroupService.GetAllGroupsAsync();
            return Ok(groups);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting customer groups.");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // POST: api/CustomerGroup
    [HttpPost]
    public async Task<ActionResult<CustomerGroup>> PostCustomerGroup(CustomerGroup customerGroup)
    {
        try
        {
            var newGroup = await _customerGroupService.AddGroupAsync(customerGroup);
            // Returns a 201 Created status with the new group
            return CreatedAtAction(nameof(GetCustomerGroups), new { id = newGroup.Id }, newGroup);
        }
        catch (InvalidOperationException ex) // For duplicate names
        {
            return Conflict(ex.Message); // Returns a 409 Conflict
        }
        catch (NotSupportedException ex) // For blocked SAP operations
        {
            return StatusCode(405, ex.Message); // Returns a 405 Method Not Allowed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while creating a customer group.");
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // PUT: api/CustomerGroup/5
    [HttpPut("{id}")]
    public async Task<IActionResult> PutCustomerGroup(int id, CustomerGroup customerGroup)
    {
        try
        {
            await _customerGroupService.UpdateGroupAsync(id, customerGroup);
            return NoContent(); // Returns a 204 No Content on success
        }
        catch (ArgumentException ex) // For ID mismatch
        {
            return BadRequest(ex.Message); // Returns a 400 Bad Request
        }
        catch (InvalidOperationException ex) // For duplicate names
        {
            return Conflict(ex.Message); // Returns a 409 Conflict
        }
        catch (NotSupportedException ex) // For blocked SAP operations
        {
            return StatusCode(405, ex.Message); // Returns a 405 Method Not Allowed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating customer group {Id}.", id);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }

    // DELETE: api/CustomerGroup/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomerGroup(int id)
    {
        try
        {
            await _customerGroupService.DeleteGroupAsync(id);
            return NoContent(); // Returns a 204 No Content on success
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ex.Message); // Returns 404 Not Found
        }
        catch (NotSupportedException ex) // For blocked SAP operations
        {
            return StatusCode(405, ex.Message); // Returns a 405 Method Not Allowed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting customer group {Id}.", id);
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }
}