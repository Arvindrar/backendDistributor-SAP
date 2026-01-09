// FILE: Controllers/SalesOrdersController.cs
using backendDistributor.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace backendDistributor.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SalesOrdersController : ControllerBase
    {
        // --- START HIGHLIGHTED CHANGE ---
        // We are replacing the direct dependency on SapService...
        // private readonly SapService _sapService;

        // ...with our new, smarter SalesOrderService.
        private readonly SalesOrderService _salesOrderService;
        private readonly ILogger<SalesOrdersController> _logger;

        public SalesOrdersController(SalesOrderService salesOrderService, ILogger<SalesOrdersController> logger)
        {
            _salesOrderService = salesOrderService;
            _logger = logger;
        }
        // --- END HIGHLIGHTED CHANGE ---

        // The GET methods need to be updated to use the new service as well
        // (but we will focus on the POST method as requested).

        [HttpGet]
        public async Task<IActionResult> GetSalesOrders()
        {
            try
            {
                // --- START HIGHLIGHTED FIX ---
                // We are replacing `throw new NotImplementedException()` with a call
                // to our new service method.
                var resultJson = await _salesOrderService.GetAllAsync();
                return Content(resultJson, "application/json");
                // --- END HIGHLIGHTED FIX ---
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while getting all sales orders.");
                return StatusCode(500, new { message = $"Internal server error: {ex.Message}" });
            }
        }

        [HttpGet("{docEntry:int}")]
        public async Task<IActionResult> GetSalesOrder(int docEntry)
        {
            try
            {
                var result = await _salesOrderService.GetByIdAsync(docEntry);
                return Content(result, "application/json");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound(new { message = $"Sales Order with ID {docEntry} not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sales order {docEntry}.", docEntry);
                return StatusCode(500, new { message = $"An internal server error occurred: {ex.Message}" });
            }
        }

        // POST: api/SalesOrders
        [HttpPost]
        public async Task<IActionResult> CreateSalesOrder([FromBody] JsonElement payload)
        {
            if (payload.ValueKind == JsonValueKind.Undefined || payload.ValueKind == JsonValueKind.Null)
            {
                return BadRequest(new { message = "Request body cannot be empty." });
            }

            try
            {
                // --- START HIGHLIGHTED CHANGE ---
                // Instead of calling SapService directly, we call our new hybrid service.
                var createdOrder = await _salesOrderService.CreateAsync(payload);
                // --- END HIGHLIGHTED CHANGE ---

                // The service returns a consistent object, so we can return 201 Created.
                return StatusCode((int)HttpStatusCode.Created, createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating a sales order.");
                return StatusCode(500, new { message = $"An internal server error occurred: {ex.Message}" });
            }
        }

        // The PUT method would also be refactored to use _salesOrderService.
        [HttpPut("{docEntry:int}")]
        public async Task<IActionResult> UpdateSalesOrder(int docEntry, [FromBody] JsonElement payload)
        {
            try
            {
                await _salesOrderService.UpdateAsync(docEntry, payload);
                return NoContent(); // Success
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound(new { message = $"Sales Order with ID {docEntry} not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while updating sales order {docEntry}.", docEntry);
                return StatusCode(500, new { message = $"An internal server error occurred: {ex.Message}" });
            }
        }
    }
}