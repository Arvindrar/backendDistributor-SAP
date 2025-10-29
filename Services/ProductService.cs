using backendDistributor.Models;
using backendDistributor.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace backendDistributor.Services
{
    public class ProductService
    {
        private readonly CustomerDbContext _context;
        private readonly SapService _sapService;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<ProductService> _logger;
        private readonly string _dataSource;

        public ProductService(
            CustomerDbContext context,
            SapService sapService,
            IConfiguration configuration,
            IWebHostEnvironment hostingEnvironment,
            ILogger<ProductService> logger)
        {
            _context = context;
            _sapService = sapService;
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
            _dataSource = configuration.GetValue<string>("DataSource") ?? "SQL";
        }

        public async Task<IEnumerable<object>> GetAllAsync(string? groupName, string? searchTerm)
        {
            if (_dataSource.Equals("SAP", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("--> ProductService: Getting all products from SAP (Efficient Single-Call method).");

                string? groupCode = null; // Add your group lookup logic here if needed

                if (!string.IsNullOrEmpty(groupName) && !groupName.Equals("All Groups", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("--> Searching for group code for group name: {GroupName}", groupName);
                    // Fetch all product groups from SAP.
                    var groupsJson = await _sapService.GetProductGroupsAsync();
                    using var groupsDoc = JsonDocument.Parse(groupsJson);

                    if (groupsDoc.RootElement.TryGetProperty("value", out var groupsElement))
                    {
                        // Find the group that matches the name (case-insensitive).
                        var foundGroup = groupsElement.EnumerateArray()
                            .FirstOrDefault(g =>
                                g.TryGetProperty("GroupName", out var nameElement) &&
                                nameElement.GetString()?.Equals(groupName, StringComparison.OrdinalIgnoreCase) == true);

                        // If we found a match, get its numeric code.
                        if (foundGroup.ValueKind != JsonValueKind.Undefined && foundGroup.TryGetProperty("Number", out var numberElement))
                        {
                            groupCode = numberElement.GetInt32().ToString();
                            _logger.LogInformation("--> Found group code '{GroupCode}' for group name '{GroupName}'", groupCode, groupName);
                        }
                        else
                        {
                            _logger.LogWarning("--> Could not find a group code for group name: {GroupName}", groupName);
                        }
                    }
                }
                // STEP 1: Get the list of all products WITH prices included.
                var sapJsonResult = await _sapService.GetProductsAsync(groupCode, searchTerm);
                using var jsonDoc = JsonDocument.Parse(sapJsonResult);

                if (!jsonDoc.RootElement.TryGetProperty("value", out var sapItemsElement))
                {
                    return Enumerable.Empty<Product>();
                }

                var productList = new List<Product>();

                // ===================================================================================
                // THE FIX IS HERE: A single, simple loop. No more parallel tasks.
                // ===================================================================================
                foreach (var item in sapItemsElement.EnumerateArray())
                {
                    // ===================================================================================
                    // THE FIX IS HERE: This helper function is now robust against null price values.
                    // ===================================================================================
                    decimal? GetPrice(int priceListId)
                    {
                        if (item.TryGetProperty("ItemPrices", out var pricesElement))
                        {
                            var priceObj = pricesElement.EnumerateArray().FirstOrDefault(p => p.GetProperty("PriceList").GetInt32() == priceListId);
                            // Check if the "Price" property exists AND its value is NOT null before trying to read it.
                            if (priceObj.ValueKind != JsonValueKind.Undefined &&
                                priceObj.TryGetProperty("Price", out var priceProp) &&
                                priceProp.ValueKind != JsonValueKind.Null)
                            {
                                return priceProp.GetDecimal();
                            }
                        }
                        return null;
                    }


                    var product = new Product
                    {
                        Id = 0,
                        SKU = item.TryGetProperty("ItemCode", out var code) ? code.GetString() : "",
                        Name = item.TryGetProperty("ItemName", out var name) ? name.GetString() : "",
                        UOM = item.TryGetProperty("InventoryUOM", out var uom) ? uom.GetString() : "",
                        HSN = item.TryGetProperty("U_HS_Code", out var hsn) && hsn.ValueKind != JsonValueKind.Null ? hsn.GetString() : null,
                        Group = groupName ?? "N/A",
                        ImageFileName = item.TryGetProperty("Picture", out var pic) && pic.ValueKind != JsonValueKind.Null ? pic.GetString() : null,
                        // Get prices directly from the item data
                        RetailPrice = GetPrice(2),
                        WholesalePrice = GetPrice(1)
                    };
                    productList.Add(product);
                }
                return productList;
            }
            else
            {
                _logger.LogInformation("--> ProductService: Getting all products from SQL.");
                var query = _context.Products.AsQueryable();
                // Your SQL logic here...
                return await query.OrderBy(p => p.Name).ToListAsync();
            }
        }
        public async Task<object> CreateProductAsync(ProductCreateDto productDto)
        {
            if (_dataSource.Equals("SAP", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("--> ProductService: Creating product in SAP and formatting response.");

                string? uniqueFileName = null; // Your image saving logic...
                if (productDto.ProductImage != null && productDto.ProductImage.Length > 0)
                {
                    // Define the path to the uploads folder
                    var uploadsFolderPath = Path.Combine(_hostingEnvironment.WebRootPath, "images", "products");
                    // Create the directory if it doesn't exist
                    if (!Directory.Exists(uploadsFolderPath))
                    {
                        Directory.CreateDirectory(uploadsFolderPath);
                    }

                    // Generate a unique file name to prevent overwriting existing files
                    uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(productDto.ProductImage.FileName);
                    var filePath = Path.Combine(uploadsFolderPath, uniqueFileName);

                    // Save the file to the server
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await productDto.ProductImage.CopyToAsync(stream);
                    }
                    _logger.LogInformation("--> Successfully saved product image as {FileName}", uniqueFileName);
                }


                var sapResponseJson = await _sapService.CreateProductAsync(productDto, uniqueFileName);

                // ===================================================================================
                // THE FIX IS HERE: Manually build the 'Product' object that the frontend expects.
                // ===================================================================================
                using var jsonDoc = JsonDocument.Parse(sapResponseJson);
                var root = jsonDoc.RootElement;

                decimal? GetPriceFromResponse(int priceListId)
                {
                    if (root.TryGetProperty("ItemPrices", out var pricesElement))
                    {
                        var priceObj = pricesElement.EnumerateArray().FirstOrDefault(p => p.GetProperty("PriceList").GetInt32() == priceListId);
                        if (priceObj.ValueKind != JsonValueKind.Undefined &&
                            priceObj.TryGetProperty("Price", out var priceProp) &&
                            priceProp.ValueKind != JsonValueKind.Null)
                        {
                            return priceProp.GetDecimal();
                        }
                    }
                    return null;
                }

                var newProduct = new Product
                {
                    Id = 0, // Not applicable for SAP items in this context
                    SKU = root.TryGetProperty("ItemCode", out var code) ? code.GetString() : "",
                    Name = root.TryGetProperty("ItemName", out var name) ? name.GetString() : "",
                    UOM = root.TryGetProperty("InventoryUOM", out var uom) ? uom.GetString() : "",
                    HSN = root.TryGetProperty("U_HS_Code", out var hsn) && hsn.ValueKind != JsonValueKind.Null ? hsn.GetString() : null,
                    Group = productDto.ProductGroup,
                    ImageFileName = root.TryGetProperty("Picture", out var pic) && pic.ValueKind != JsonValueKind.Null ? pic.GetString() : null,
                    RetailPrice = GetPriceFromResponse(2),
                    WholesalePrice = GetPriceFromResponse(1)
                };

                return newProduct; // Return the properly formatted object
            }
            else // Your existing SQL logic is now fully restored
            {
                _logger.LogInformation("--> ProductService: Creating product in SQL.");

                if (await _context.Products.AnyAsync(p => p.SKU == productDto.SKU))
                {
                    throw new InvalidOperationException($"A product with SKU '{productDto.SKU}' already exists in the SQL database.");
                }

                // ========================================================================
                // THE FIX IS HERE: The code to define the 'product' variable is restored.
                // ========================================================================
                var product = new Product
                {
                    SKU = productDto.SKU,
                    Name = productDto.ProductName,
                    Group = productDto.ProductGroup,
                    UOM = productDto.UOM,
                    HSN = productDto.HSN,
                    UOMGroup = productDto.UOMGroup, // Assumes Product.UOMGroup is a string?
                };

                if (decimal.TryParse(productDto.RetailPrice, out decimal retailP)) product.RetailPrice = retailP;
                if (decimal.TryParse(productDto.WholesalePrice, out decimal wholesaleP)) product.WholesalePrice = wholesaleP;

                if (productDto.ProductImage != null && productDto.ProductImage.Length > 0)
                {
                    var uploadsFolderPath = Path.Combine(_hostingEnvironment.WebRootPath, "images", "products");
                    if (!Directory.Exists(uploadsFolderPath)) Directory.CreateDirectory(uploadsFolderPath);

                    var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(productDto.ProductImage.FileName);
                    var filePath = Path.Combine(uploadsFolderPath, uniqueFileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await productDto.ProductImage.CopyToAsync(stream);
                    }
                    product.ImageFileName = uniqueFileName;
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();
                return product; // This line now works because 'product' is defined above.
            }
        }
        // public async Task<object> GetProductByIdAsync(string id) { ... }
        // public async Task<IEnumerable<object>> GetAllProductsAsync() { ... }
    }
}