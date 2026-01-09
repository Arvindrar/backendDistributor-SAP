// File: Controllers/CustomerController.cs
using backendDistributor.Models;
using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace backendDistributor.Controllers
{
    [Route("api/Customers")]
    [ApiController]
    public class CustomerController : ControllerBase
    {
        private readonly CustomerService _customerService;
        private readonly ILogger<CustomerController> _logger;

        public CustomerController(CustomerService customerService, ILogger<CustomerController> logger)
        {
            _customerService = customerService;
            _logger = logger;
        }

        // GET: api/Customer
        [HttpGet]
        public async Task<IActionResult> GetCustomers(
            [FromQuery] string? group,
            [FromQuery] string? searchTerm,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 1000) // <-- CHANGED FROM 8 to 1000
        {
            try
            {
                var sapJsonResult = await _customerService.GetAllAsync(group, searchTerm, pageNumber, pageSize);
                return Content(sapJsonResult, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get customers.");
                return StatusCode(500, new { message = "An internal server error occurred." });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCustomer(string id)
        {
            var customer = await _customerService.GetByCardCodeAsync(id);

            if (customer == null)
            {
                return NotFound();
            }

            return Ok(customer);
        }

        // POST: api/Customer
        [HttpPost]
        public async Task<IActionResult> CreateCustomer([FromBody] JsonElement customerData)
        {
            _logger.LogInformation("--- RECEIVED PAYLOAD FROM FRONTEND ---\n{Payload}\n---------------------------------", customerData.ToString());

            if (customerData.ValueKind == JsonValueKind.Undefined || string.IsNullOrEmpty(customerData.ToString()))
            {
                return BadRequest(new { message = "Request body cannot be empty." });
            }

            try
            {
                var createdCustomer = await _customerService.AddAsync(customerData);

                // Use the CardCode from the response as the ID for the CreatedAtAction route
                string? cardCode = createdCustomer.TryGetProperty("CardCode", out var code) ? code.GetString() : null;

                // We can't use CreatedAtAction easily without a standard GetById, so we return 201 Created with the object
                return StatusCode((int)HttpStatusCode.Created, createdCustomer);
            }
            catch (HttpRequestException httpEx) // Catch specific SAP errors
            {
                _logger.LogError(httpEx, "SAP Service Layer returned an error during customer creation.");
                // Try to parse the error from SAP's response
                object? errorDetails = httpEx.Message;
                try { errorDetails = JsonSerializer.Deserialize<object>(httpEx.Message); } catch { }
                return StatusCode((int)(httpEx.StatusCode ?? HttpStatusCode.BadGateway), errorDetails);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a customer.");
                return StatusCode(500, new { message = $"An internal server error occurred: {ex.Message}" });
            }
        }
    }
}