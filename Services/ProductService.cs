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
                _logger.LogInformation("--> ProductService: Getting all products from SAP.");

                string? groupCode = null; // Group lookup logic is fine
                // ...

                var sapJsonResult = await _sapService.GetProductsAsync(groupCode, searchTerm);
                using var jsonDoc = JsonDocument.Parse(sapJsonResult);

                if (jsonDoc.RootElement.TryGetProperty("value", out var sapItemsElement))
                {
                    // THE FIX: Deserialize the entire array at once.
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var productList = sapItemsElement.Deserialize<List<Product>>(options);

                    if (productList != null)
                    {
                        // After deserializing, loop through to set the prices
                        foreach (var product in productList)
                        {
                            product.RetailPrice = product.ItemPrices?.FirstOrDefault(p => p.PriceList == 2)?.Price;
                            product.WholesalePrice = product.ItemPrices?.FirstOrDefault(p => p.PriceList == 1)?.Price;
                        }
                        return productList;
                    }
                }

                return Enumerable.Empty<Product>();
            }
            else
            {
                _logger.LogInformation("--> ProductService: Getting all products from SQL.");
                var query = _context.Products.AsQueryable();
                // Your SQL logic here...
                return await query.OrderBy(p => p.Name).ToListAsync();
            }
        }


        public async Task<Product?> GetBySkuAsync(string sku)
        {
            if (_dataSource.Equals("SAP", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("--> ProductService: Getting single product from SAP by SKU.");
                // We can reuse the existing GetAllAsync and filter in memory,
                // or create a specific GetProductBySku in SapService.
                // For simplicity, let's filter here.
                var allProducts = (await GetAllAsync(null, sku)) as IEnumerable<Product>;
                return allProducts?.FirstOrDefault(p => p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                _logger.LogInformation("--> ProductService: Getting single product from SQL by SKU.");
                return await _context.Products.FirstOrDefaultAsync(p => p.SKU == sku);
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