using Microsoft.AspNetCore.Mvc;
using backendDistributor.Services; // Use our new service
using System.Text.Json; // Required for JsonElement

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly SapService _sapService;
        private readonly ILogger<CustomerController> _logger;

        // We inject SapService instead of a database context
        public CustomerController(SapService sapService, ILogger<CustomerController> logger)
        {
            _sapService = sapService;
            _logger = logger;
        }

        // GET: api/Customer
        [HttpGet]
        public async Task<IActionResult> GetCustomers(
    [FromQuery] string? group,
    [FromQuery] string? searchTerm,
    [FromQuery] int pageNumber = 1,  // Default to page 1
    [FromQuery] int pageSize = 20)   // Default to 20 items per page
        {
            try
            {
                var sapJsonResult = await _sapService.GetCustomersAsync(group, searchTerm, pageNumber, pageSize);
                return Content(sapJsonResult, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customers from SAP.");
                return StatusCode(500, new { message = $"An error occurred while communicating with SAP: {ex.Message}" });
            }
        }

        // GET: api/Customer/C00001
        // The id here will be the CardCode, which is a string
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(string id)
        {
            try
            {
                var sapJsonResult = await _sapService.GetCustomerByIdAsync(id);
                return Content(sapJsonResult, "application/json");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return NotFound(new { message = $"Customer with CardCode '{id}' not found in SAP." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching customer {id} from SAP.", id);
                return StatusCode(500, new { message = $"An error occurred while getting customer {id}: {ex.Message}" });
            }
        }


        // POST: api/Customer
        // The 'customer' parameter will be raw JSON from the frontend
        [HttpPost]
        public async Task<IActionResult> PostCustomer([FromBody] JsonElement customer)
        {
            try
            {
                var createdCustomerJson = await _sapService.CreateCustomerAsync(customer);
                // Return 201 Created with the response from SAP
                return StatusCode(201, JsonDocument.Parse(createdCustomerJson));
            }
            catch (HttpRequestException ex)
            {
                // Try to parse the SAP error for a better frontend message
                try
                {
                    var sapErrorDoc = JsonDocument.Parse(ex.Message);
                    var errorMessage = sapErrorDoc.RootElement.GetProperty("error").GetProperty("message").GetProperty("value").GetString();
                    return BadRequest(new { message = errorMessage });
                }
                catch
                {
                    // If parsing fails, return the raw error
                    return BadRequest(new { message = ex.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer in SAP.");
                return StatusCode(500, new { message = $"An internal error occurred: {ex.Message}" });
            }
        }

        // PUT: api/Customer/C00001
        // We use PUT as the HTTP verb for consistency, even though SAP uses PATCH underneath
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCustomer(string id, [FromBody] JsonElement customer)
        {
            try
            {
                // Note: The 'id' in the URL is the CardCode, which cannot be changed.
                // The payload 'customer' should not contain the CardCode. SAP ignores it anyway on update.
                await _sapService.UpdateCustomerAsync(id, customer);
                return NoContent(); // 204 No Content is the standard response for a successful update
            }
            catch (HttpRequestException ex)
            {
                try
                {
                    var sapErrorDoc = JsonDocument.Parse(ex.Message);
                    var errorMessage = sapErrorDoc.RootElement.GetProperty("error").GetProperty("message").GetProperty("value").GetString();
                    return BadRequest(new { message = errorMessage });
                }
                catch
                {
                    return BadRequest(new { message = ex.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {id} in SAP.", id);
                return StatusCode(500, new { message = $"An internal error occurred while updating customer {id}: {ex.Message}" });
            }
        }

        // DELETE: api/Customer/C00001
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(string id)
        {
            try
            {
                await _sapService.DeleteCustomerAsync(id);
                return NoContent(); // 204 No Content for successful deletion
            }
            catch (HttpRequestException ex)
            {
                try
                {
                    var sapErrorDoc = JsonDocument.Parse(ex.Message);
                    var errorMessage = sapErrorDoc.RootElement.GetProperty("error").GetProperty("message").GetProperty("value").GetString();
                    return BadRequest(new { message = errorMessage });
                }
                catch
                {
                    return BadRequest(new { message = ex.Message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer {id} in SAP.", id);
                return StatusCode(500, new { message = $"An internal error occurred while deleting customer {id}: {ex.Message}" });
            }
        }
    }
}