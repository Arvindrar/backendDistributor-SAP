// Controllers/SalesEmployeeController.cs
using backendDistributor.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Threading.Tasks;

[Route("api/[controller]")]
[ApiController]
public class SalesEmployeeController : ControllerBase
{
    private readonly SalesEmployeeService _employeeService;
    private readonly ILogger<SalesEmployeeController> _logger;
    private readonly CustomerDbContext _context; // The declaration for the context

    public SalesEmployeeController(
        SalesEmployeeService employeeService,
        ILogger<SalesEmployeeController> logger,
        CustomerDbContext context) // Step 2: Add CustomerDbContext as a parameter
    {
        // Step 3: Assign the injected services to your private fields.
        // The error you see happens if the line `_context = context;` is missing.
        _employeeService = employeeService;
        _logger = logger;
        _context = context; // <<< THIS LINE IS THE FIX. MAKE SURE IT'S HERE.
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
    public async Task<ActionResult<SalesEmployee>> PostSalesEmployee(SalesEmployee salesEmployee)
    {
        // --- FIX #1: VALIDATE THE MODEL STATE ---
        // This checks for [Required], [StringLength], [EmailAddress], etc.
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState); // Returns a 400 error with validation details
        }

        // --- FIX #2: BUSINESS RULE VALIDATION (CHECK FOR DUPLICATES) ---
        var existingByName = await _context.SalesEmployees.FirstOrDefaultAsync(e => e.Name.ToLower() == salesEmployee.Name.ToLower());
        if (existingByName != null)
        {
            // Add error to ModelState and return a 400 Bad Request
            ModelState.AddModelError("Name", "A sales employee with this name already exists.");
            return BadRequest(ModelState);
        }

        try
        {
            var newEmployee = await _employeeService.AddAsync(salesEmployee);
            return CreatedAtAction(nameof(GetSalesEmployee), new { id = newEmployee.Id }, newEmployee);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating new sales employee.");
            return StatusCode(500, new { message = "An error occurred while creating the employee." });
        }
    }

    // In your PUT method
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSalesEmployee(int id, SalesEmployee salesEmployee)
    {
        if (id != salesEmployee.Id) return BadRequest("ID mismatch.");
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            // ... (your duplicate check logic here) ...
            await _employeeService.UpdateAsync(id, salesEmployee);
            return NoContent(); // Success
        }
        catch (HttpRequestException sapEx) // Catch specific SAP errors
        {
            _logger.LogError(sapEx, "An error occurred during SAP update.");
            return StatusCode(502, new { message = "Failed to update record in SAP.", details = sapEx.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (Exception ex) // Catch all other unexpected errors
        {
            _logger.LogError(ex, $"An unexpected error occurred updating employee {id}.");
            return StatusCode(500, new { message = "An internal server error occurred." });
        }
    }
    // DELETE: api/SalesEmployee/5
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSalesEmployee(int id)
    {
        await _employeeService.DeleteAsync(id);
        return NoContent();
    }
}