// REPLACE THE ENTIRE CONTENT of Controllers/VendorController.cs with this code

using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VendorController : ControllerBase
    {
        private readonly VendorService _vendorService;
        private readonly ILogger<VendorController> _logger;

        public VendorController(VendorService vendorService, ILogger<VendorController> logger)
        {
            _vendorService = vendorService;
            _logger = logger;
        }

        // GET: api/Vendor (List view)
        [HttpGet]
        public async Task<IActionResult> GetVendors(
            [FromQuery] string? group,
            [FromQuery] string? searchTerm,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 8)
        {
            try
            {
                var sapJsonResult = await _vendorService.GetAllAsync(group, searchTerm, pageNumber, pageSize);
                return Content(sapJsonResult, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get vendors.");
                return StatusCode(500, new { message = "An internal server error occurred while fetching vendors." });
            }
        }

        [HttpGet("{cardCode}")]
        public async Task<IActionResult> GetVendor(string cardCode)
        {
            try
            {
                var vendor = await _vendorService.GetByCardCodeAsync(cardCode);
                if (vendor == null)
                {
                    return NotFound(new { message = $"Vendor with CardCode '{cardCode}' not found." });
                }
                return Ok(vendor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting vendor by CardCode {CardCode}", cardCode);
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }

        // POST: api/Vendor (Create new vendor)
        [HttpPost]
        public async Task<IActionResult> CreateVendor([FromBody] JsonElement vendorData)
        {
            _logger.LogInformation("--- RECEIVED VENDOR PAYLOAD FROM FRONTEND ---\n{Payload}\n---------------------------------", vendorData.ToString());

            if (vendorData.ValueKind == JsonValueKind.Undefined)
            {
                return BadRequest(new { message = "Request body cannot be empty." });
            }

            try
            {
                var createdVendor = await _vendorService.AddAsync(vendorData);
                return StatusCode((int)HttpStatusCode.Created, createdVendor);
            }
            catch (HttpRequestException httpEx) // Catch specific SAP errors
            {
                _logger.LogError(httpEx, "SAP Service Layer returned an error during vendor creation.");
                object? errorDetails = httpEx.Message;
                try { errorDetails = JsonSerializer.Deserialize<object>(httpEx.Message); } catch { }
                return StatusCode((int)(httpEx.StatusCode ?? HttpStatusCode.BadGateway), errorDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a vendor.");
                return StatusCode(500, new { message = $"An internal server error occurred: {ex.Message}" });
            }
        }

        // PUT: api/Vendor/V123 (Update existing vendor)
        [HttpPut("{cardCode}")]
        public async Task<IActionResult> UpdateVendor(string cardCode, [FromBody] JsonElement vendorData)
        {
            if (string.IsNullOrEmpty(cardCode))
            {
                return BadRequest(new { message = "Vendor CardCode is required for an update." });
            }
            try
            {
                await _vendorService.UpdateAsync(cardCode, vendorData);
                return NoContent(); // 204 is standard for a successful update
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "SAP Service Layer returned an error during vendor update.");
                object? errorDetails = httpEx.Message;
                try { errorDetails = JsonSerializer.Deserialize<object>(httpEx.Message); } catch { }
                return StatusCode((int)(httpEx.StatusCode ?? HttpStatusCode.BadGateway), errorDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating a vendor.");
                return StatusCode(500, new { message = $"An internal server error occurred: {ex.Message}" });
            }
        }
    }
}