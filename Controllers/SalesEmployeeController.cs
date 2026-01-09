using backendDistributor.Models;
using Microsoft.AspNetCore.Mvc;
using backendDistributor.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

[Route("api/[controller]")]
[ApiController]
public class SalesEmployeeController : ControllerBase
{
    private readonly SalesEmployeeService _employeeService;
    private readonly ILogger<SalesEmployeeController> _logger;
    private readonly CustomerDbContext _context;
    private readonly IConfiguration _configuration;

    public SalesEmployeeController(
        SalesEmployeeService employeeService,
        ILogger<SalesEmployeeController> logger,
        CustomerDbContext context,
        IConfiguration configuration)
    {
        _employeeService = employeeService;
        _logger = logger;
        _context = context;
        _configuration = configuration;
    }

    // GET: api/SalesEmployee
    [HttpGet]
    public async Task<ActionResult<IEnumerable<SalesEmployee>>> GetSalesEmployees()
    {
        try
        {
            var employees = await _employeeService.GetAllAsync();
            return Ok(employees);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while getting sales employees.");
            return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
        }
    }

    // GET: api/SalesEmployee/5
    [HttpGet("{id}")]
    public async Task<ActionResult<SalesEmployee>> GetSalesEmployee(int id)
    {
        var employee = await _employeeService.GetByIdAsync(id);
        if (employee == null) return NotFound();
        return Ok(employee);
    }

    // POST: api/SalesEmployee
    [HttpPost]
    public async Task<ActionResult<SalesEmployee>> PostSalesEmployee(SalesEmployeeCreateDto dto)
    {
        // Now, ModelState.IsValid works perfectly against the DTO.
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // --- Map the DTO to your internal SalesEmployee model ---
        var salesEmployee = new SalesEmployee
        {
            Code = dto.Code,
            Name = dto.Name,
            ContactNumber = dto.ContactNumber,
            Email = dto.Email,
            Remarks = dto.Remarks
        };

        // SQL duplicate checks (if needed) remain the same
        var dataSource = _configuration.GetValue<string>("DataSource");
        if (dataSource?.Equals("SQL", StringComparison.OrdinalIgnoreCase) == true)
        {
            // ... (SQL duplicate check logic is fine)
        }

        try
        {
            // The service layer receives the clean, validated model
            var newEmployee = await _employeeService.AddAsync(salesEmployee);
            return CreatedAtAction(nameof(GetSalesEmployee), new { id = newEmployee.Id }, newEmployee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new sales employee.");
            return BadRequest(new { message = ex.Message });
        }
    }
    // PUT: api/SalesEmployee/5
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSalesEmployee(int id, SalesEmployee salesEmployee)
    {
        // ... Your existing, correct PUT method ...
        if (id != salesEmployee.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            await _employeeService.UpdateAsync(id, salesEmployee);
            return NoContent();
        }
        catch (KeyNotFoundException) { return NotFound(); }
        // ... etc.
    }


    // --- THIS IS THE NEW, ROBUST DELETE METHOD ---
    // DELETE: api/SalesEmployee/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSalesEmployee(int id)
    {
        try
        {
            // This service call will correctly route to SAP or SQL
            await _employeeService.DeleteAsync(id);
            // A successful DELETE should return 204 No Content
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            // This is thrown by our service if the employee ID doesn't exist.
            _logger.LogWarning("Attempted to delete non-existent Sales Employee with ID: {Id}", id);
            return NotFound(new { message = $"Sales employee with ID {id} not found." });
        }
        catch (HttpRequestException sapEx)
        {
            // This catches specific errors from the SapService, like SAP refusing the delete.
            _logger.LogError(sapEx, "A SAP-specific error occurred while deleting Sales Employee with ID: {Id}", id);
            // Return a 400 Bad Request so the frontend can display the specific SAP error message.
            return BadRequest(new { message = $"SAP Deletion Failed: {sapEx.Message}" });
        }
        catch (Exception ex)
        {
            // This catches all other unexpected errors (e.g., SQL database offline).
            _logger.LogError(ex, "An unexpected error occurred while deleting Sales Employee with ID: {Id}", id);
            return StatusCode(500, new { message = "An internal server error occurred during deletion." });
        }
    }
}