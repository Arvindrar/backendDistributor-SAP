using backendDistributor.Dtos;
using backendDistributor.Models;
using backendDistributor.Models.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data.Common;
using System.Drawing.Printing;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;


namespace backendDistributor.Services // Make sure this namespace matches your project
{
    public class SapService
    {
        // --- MODIFIED PRIVATE FIELDS ---
        private readonly HttpClient _httpClient; // This will be injected
        private readonly SapServiceLayerSettings _settings;
        private readonly ILogger<SapService> _logger;
        private readonly SapCookieContainer _sapCookieContainer;


        // --- THIS IS THE NEW CONSTRUCTOR ---
        public SapService(
            IHttpClientFactory httpClientFactory, // <-- Use the factory to get the named client
            SapCookieContainer sapCookieContainer,
            IOptions<SapServiceLayerSettings> settings,
            ILogger<SapService> logger)
        {
            // Get the fully-configured client we defined in Program.cs
            _httpClient = httpClientFactory.CreateClient("SapClient");
            _sapCookieContainer = sapCookieContainer; // Assign the injected container
            _settings = settings.Value;
            _logger = logger;
            // All the manual HttpClient creation code is now gone!
        }

        private async Task EnsureLoginAsync()
        {
            // Use the injected services to check for the cookie
            // The BaseAddress comes from the pre-configured _httpClient
            var cookies = _sapCookieContainer.Container?.GetCookies(_httpClient.BaseAddress!);
            if (cookies?["B1SESSION"] == null)
            {
                _logger.LogWarning("No active SAP session found. Initiating new login.");
                await LoginAsync();
            }
            else
            {
                _logger.LogInformation("Active SAP session found. Reusing session.");
            }
        }
        private async Task LoginAsync()
        {
            var loginPayload = new
            {
                CompanyDB = _settings.CompanyDB,
                UserName = _settings.UserName,
                Password = _settings.Password
            };

            var sapJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // This preserves PascalCase
            };
            var content = new StringContent(JsonSerializer.Serialize(loginPayload, sapJsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("Login", content);

            // The HttpClientHandler configured in Program.cs will automatically place the cookie
            // into our shared _sapCookieContainer.Container. No extra work is needed here.

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SAP Login failed. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException($"SAP login failed: {errorContent}", null, response.StatusCode);
            }
            _logger.LogInformation("Successfully logged in to SAP.");
        }

        // In your Services/SapService.cs file

        /// This is our gatekeeper method. It ensures we are logged in before every single SAP request.
        /// </summary>
        // --- PASTE THIS ENTIRE CORRECTED METHOD INTO SapService.cs ---
        // --- PASTE THIS ENTIRE CORRECTED METHOD INTO SapService.cs ---
        public async Task<string> GetCustomersAsync(string? group, string? searchTerm, int pageNumber, int pageSize)
        {
            await EnsureLoginAsync();

            var filterClauses = new List<string> { "CardType eq 'cCustomer'" };

            if (!string.IsNullOrEmpty(group))
            {
                filterClauses.Add($"GroupCode eq {group}");
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                // ============================================================================
                //  THE FIX IS HERE (Part 1):
                //  Remove .ToLower(). We will perform a case-sensitive search.
                // ============================================================================
                var sanitizedTerm = searchTerm.Trim().Replace("'", "''");

                if (!string.IsNullOrEmpty(sanitizedTerm))
                {
                    // ============================================================================
                    //  THE FIX IS HERE (Part 2):
                    //  Remove the tolower() function from the query string.
                    //  This is necessary because the SAP OData parser is rejecting the nested function.
                    // ============================================================================
                    var nameFilter = $"(CardName ne null and substringof('{sanitizedTerm}', CardName))";
                    var codeFilter = $"(CardCode ne null and substringof('{sanitizedTerm}', CardCode))";

                    filterClauses.Add($"({nameFilter} or {codeFilter})");
                }
            }

            var filterQuery = string.Join(" and ", filterClauses);
            var encodedFilter = WebUtility.UrlEncode(filterQuery);
            int skip = (pageNumber - 1) * pageSize;

            var fields = "CardCode,CardName,CurrentAccountBalance,SalesPersonCode,BPAddresses,Notes,CreditLimit,Territory";
            var expand = "SalesPerson($select=SalesEmployeeName)";

            var requestUrl = $"BusinessPartners?$filter={encodedFilter}&$select={fields}&$expand={expand}&$skip={skip}&$top={pageSize}&$count=true";

            _logger.LogInformation("SAP Customer Query URL: {Url}", requestUrl);

            var response = await _httpClient.GetAsync(requestUrl);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogInformation("SAP session expired. Attempting to re-login.");
                await LoginAsync();
                response = await _httpClient.GetAsync(requestUrl);
            }

            // This is the line (137) that was throwing the exception.
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> GetCustomerByIdAsync(string cardCode)
        {
            await EnsureLoginAsync();
            // The CardCode needs to be in single quotes in the URL
            var requestUrl = $"BusinessPartners('{cardCode}')";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> CreateCustomerAsync(JsonElement customerData)
        {
            await EnsureLoginAsync(); // This guarantees we are logged in

            var content = new StringContent(customerData.ToString(), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("BusinessPartners", content);

            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to create SAP Business Partner. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, responseContent);
                throw new HttpRequestException(responseContent, null, response.StatusCode);
            }

            _logger.LogInformation("Successfully created SAP Business Partner.");
            return responseContent;
        }

        public async Task UpdateCustomerAsync(string cardCode, JsonElement customerData)
        {
            await EnsureLoginAsync();

            // SAP uses PATCH for updates, not PUT
            var requestUrl = $"BusinessPartners('{cardCode}')";
            var content = new StringContent(customerData.ToString(), Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update SAP Business Partner. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }

            // A successful PATCH returns 204 No Content, so there is no body to return.
        }

        public async Task DeleteCustomerAsync(string cardCode)
        {
            await EnsureLoginAsync();

            var requestUrl = $"BusinessPartners('{cardCode}')";
            var response = await _httpClient.DeleteAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete SAP Business Partner. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
            // A successful DELETE returns 204 No Content.
        }

        // --- PASTE THIS ENTIRE CORRECTED METHOD INTO SapService.cs ---
        public async Task<string> GetRoutesAsync()
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Fetching ALL Routes (Territories) from SAP, correctly handling pagination.");

            var allTerritoryElements = new List<JsonElement>();
            string? nextLink = "Territories"; // The initial endpoint

            while (!string.IsNullOrEmpty(nextLink))
            {
                _logger.LogInformation("Fetching Territories page: {PageUrl}", nextLink);
                var response = await _httpClient.GetAsync(nextLink);

                // This will throw an exception if the API call fails, which is what we want.
                response.EnsureSuccessStatusCode();

                var pageContent = await response.Content.ReadAsStringAsync();
                using (var jsonDoc = JsonDocument.Parse(pageContent))
                {
                    var root = jsonDoc.RootElement;

                    if (root.TryGetProperty("value", out var valueElement))
                    {
                        // CRITICAL: We must clone each element before adding it to our list,
                        // because the JsonDocument will be disposed at the end of the loop.
                        foreach (var element in valueElement.EnumerateArray())
                        {
                            allTerritoryElements.Add(element.Clone());
                        }
                    }

                    // Check if SAP provided a link to the next page of results
                    if (root.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                    {
                        var fullNextLink = nextLinkElement.GetString();
                        // HttpClient needs the relative path, not the full URL SAP sends.
                        nextLink = _httpClient.BaseAddress != null
                            ? fullNextLink?.Replace(_httpClient.BaseAddress.ToString(), "")
                            : fullNextLink;
                    }
                    else
                    {
                        // No more pages, so we exit the loop.
                        nextLink = null;
                    }
                }
            }

            _logger.LogInformation("Successfully fetched a total of {Count} Territories from all pages.", allTerritoryElements.Count);

            // Reconstruct the final JSON to look like a single, complete SAP response.
            var finalResult = new { value = allTerritoryElements };
            return JsonSerializer.Serialize(finalResult);
        }
        public async Task<string> CreateRouteAsync(JsonElement routeData)
        {
            await EnsureLoginAsync();

            // The frontend sends { "name": "..." }, but SAP expects { "Description": "..." }
            // We create the correct payload here.
            var sapPayload = new { Description = routeData.GetProperty("name").GetString() };
            var content = new StringContent(JsonSerializer.Serialize(sapPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("Territories", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create SAP Territory. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task UpdateRouteAsync(int routeId, JsonElement routeData)
        {
            await EnsureLoginAsync();

            // The key for Territories is the TerritoryID. It must be in parentheses.
            var requestUrl = $"Territories({routeId})";

            var content = new StringContent(routeData.ToString(), System.Text.Encoding.UTF8, "application/json");

            // SAP uses PATCH for updates.
            var response = await _httpClient.PatchAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update SAP Territory. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                // This will be caught by the controller and turned into a 4xx or 5xx response.
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
            // A successful PATCH returns 204 No Content, so there is no body to return.
        }

        // === NEW: Method to Delete a Route (Territory) from SAP ===
        public async Task DeleteRouteAsync(int routeId)
        {
            await EnsureLoginAsync();

            var requestUrl = $"Territories({routeId})";
            var response = await _httpClient.DeleteAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete SAP Territory. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
            // A successful DELETE also returns 204 No Content.
        }


        public async Task<string> GetBusinessPartnerGroupsAsync()
        {
            await EnsureLoginAsync();
            var requestUrl = "BusinessPartnerGroups?$select=Code,Name,Type";
            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }


        public async Task<string> CreateBusinessPartnerGroupAsync(CustomerGroup group)
        {
            await EnsureLoginAsync();

            // SAP expects a specific JSON format.
            // We must specify the 'Type' as 'bbpgt_CustomerGroup'.
            var sapPayload = new
            {
                Name = group.Name,
                Type = "bbpgt_CustomerGroup"
            };

            var content = new StringContent(JsonSerializer.Serialize(sapPayload), System.Text.Encoding.UTF8, "application/json");

            _logger.LogInformation("Attempting to create SAP Business Partner Group with name: {Name}", group.Name);

            var response = await _httpClient.PostAsync("BusinessPartnerGroups", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create SAP Business Partner Group. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }

            // Return the response from SAP, which will include the newly created group with its ID.
            return await response.Content.ReadAsStringAsync();
        }

        // PASTE THIS NEW METHOD into your SapService.cs file

        public async Task DeleteBusinessPartnerGroupAsync(int groupId)
        {
            await EnsureLoginAsync();

            // The key for BusinessPartnerGroups is the 'Code'. It must be in parentheses.
            var requestUrl = $"BusinessPartnerGroups({groupId})";

            _logger.LogInformation("Attempting to delete SAP Business Partner Group with ID: {Id}", groupId);

            var response = await _httpClient.DeleteAsync(requestUrl);

            // A successful DELETE returns a 204 No Content response.
            // EnsureSuccessStatusCode will throw an exception if the status is not 2xx.
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete SAP Business Partner Group. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
        }
        public async Task<string> GetShippingTypesAsync()
        {
            await EnsureLoginAsync();
            var requestUrl = "ShippingTypes?$select=Code,Name";
            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> CreateShippingTypeAsync(JsonElement shippingTypeData)
        {
            await EnsureLoginAsync();

            // The frontend sends { "name": "..." }, but SAP expects { "Name": "..." }
            // We create the correct payload here. Note the capital 'N'.
            var sapPayload = new { Name = shippingTypeData.GetProperty("name").GetString() };
            var content = new StringContent(JsonSerializer.Serialize(sapPayload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("ShippingTypes", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create SAP Shipping Type. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }

            return await response.Content.ReadAsStringAsync();
        }
        public async Task DeleteShippingTypeAsync(int shippingTypeId)
        {
            await EnsureLoginAsync();

            // The key for ShippingTypes is 'Code'. It must be in parentheses.
            var requestUrl = $"ShippingTypes({shippingTypeId})";
            _logger.LogInformation("Attempting to delete SAP Shipping Type with ID: {Id}", shippingTypeId);

            var response = await _httpClient.DeleteAsync(requestUrl);

            // A successful DELETE returns a 204 No Content response.
            // We throw an exception on any other status code.
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete SAP Shipping Type. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
        }

        public async Task<string> GetSalesEmployeesAsync()
        {
            await EnsureLoginAsync();
            var requestUrl = "SalesPersons?$select=SalesEmployeeCode,SalesEmployeeName&$filter=Active eq 'tYES'";

            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        public async Task<string> GetEmployeeInfoByIdAsync(int employeeID)
        {
            await EnsureLoginAsync();
            var requestUrl = $"EmployeesInfo({employeeID})?$select=Department";
            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetSalesEmployeeByIdAsync(int salesEmployeeCode)
        {
            await EnsureLoginAsync();
            var requestUrl = $"SalesPersons({salesEmployeeCode})";
            var response = await _httpClient.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> CreateSalesEmployeeAsync(SalesEmployee employee)
        {
            await EnsureLoginAsync();
            var sapPayload = new
            {
                SalesEmployeeName = employee.Name,
                Mobile = employee.ContactNumber,
                Email = employee.Email,
                Remarks = employee.Remarks,
                Active = "tYES"
            };
            var content = new StringContent(JsonSerializer.Serialize(sapPayload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("SalesPersons", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
            return await response.Content.ReadAsStringAsync();
        }

        // File: Services/SapService.cs
        // File: Services/SapService.cs

        public async Task<JsonNode?> GetCombinedSalesEmployeeDetailsAsync(int salesEmployeeCode)
        {
            await EnsureLoginAsync();

            // 1. Get base data from SalesPersons
            var salesPersonUrl = $"SalesPersons({salesEmployeeCode})";
            var salesPersonResponse = await _httpClient.GetAsync(salesPersonUrl);
            if (!salesPersonResponse.IsSuccessStatusCode) return null;
            var salesPersonNode = JsonNode.Parse(await salesPersonResponse.Content.ReadAsStringAsync());
            if (salesPersonNode == null) return null;

            // 2. Get the linking ID
            int? employeeInfoId = (int?)salesPersonNode["EmployeeID"];
            if (!employeeInfoId.HasValue || employeeInfoId <= 0)
            {
                // No link to EmployeesInfo, just return the base data
                return salesPersonNode;
            }

            // 3. Get extra data from EmployeesInfo
            var employeeInfoUrl = $"EmployeesInfo({employeeInfoId})";
            var employeeInfoResponse = await _httpClient.GetAsync(employeeInfoUrl);
            if (employeeInfoResponse.IsSuccessStatusCode)
            {
                var employeeInfoNode = JsonNode.Parse(await employeeInfoResponse.Content.ReadAsStringAsync());
                if (employeeInfoNode != null)
                {
                    // 4. MERGE the results
                    salesPersonNode["JobTitle"] = employeeInfoNode["JobTitle"]; // The text name
                    salesPersonNode["Position"] = employeeInfoNode["Position"]; // The integer ID
                    salesPersonNode["Department"] = employeeInfoNode["DepartmentName"]; // The text name
                    salesPersonNode["Address"] = employeeInfoNode["HomeStreet"]; // The address
                }
            }
            return salesPersonNode;
        }

        public async Task UpdateSalesEmployeeAsync(int salesEmployeeCode, SalesEmployee employee)
        {
            await EnsureLoginAsync();
            var requestUrl = $"SalesPersons({salesEmployeeCode})";

            // Create a simple payload with only the fields that exist on the SalesPersons object
            var sapPayload = new
            {
                SalesEmployeeName = employee.Name,
                Mobile = employee.ContactNumber,
                Email = employee.Email,
                Remarks = employee.Remarks,
                Active = employee.IsActive ? "tYES" : "tNO"
            };

            var content = new StringContent(JsonSerializer.Serialize(sapPayload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PatchAsync(requestUrl, content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"SAP update failed for {salesEmployeeCode}: {errorContent}");
                throw new HttpRequestException($"SAP update failed: {errorContent}");
            }
        }
        public async Task DeleteSalesEmployeeAsync(int salesEmployeeCode)
        {
            await EnsureLoginAsync();
            var requestUrl = $"SalesPersons({salesEmployeeCode})";
            var response = await _httpClient.DeleteAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
        }

        // This is an example of what your SapService.cs might need to contain.
        // You will need to fill in the actual logic.

        // PASTE THIS to replace the old GetVatGroupsAsync method in SapService.cs

        public async Task<string> GetVatGroupsAsync()
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Attempting to GET all VatGroups from SAP, handling pagination.");

            var allVatGroupElements = new List<JsonElement>();
            string? nextLink = "VatGroups";

            while (!string.IsNullOrEmpty(nextLink))
            {
                _logger.LogInformation("Fetching page: {PageUrl}", nextLink);
                var response = await _httpClient.GetAsync(nextLink);
                response.EnsureSuccessStatusCode();

                var pageContent = await response.Content.ReadAsStringAsync();

                // The 'using' block ensures the JsonDocument is disposed, which is good practice.
                using (var jsonDoc = JsonDocument.Parse(pageContent))
                {
                    if (jsonDoc.RootElement.TryGetProperty("value", out var valueElement))
                    {
                        // ==========================================================
                        //  THE FIX IS HERE: We CLONE each element before adding it.
                        // ==========================================================
                        foreach (var element in valueElement.EnumerateArray())
                        {
                            allVatGroupElements.Add(element.Clone());
                        }
                    }

                    if (jsonDoc.RootElement.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                    {
                        nextLink = nextLinkElement.GetString();
                    }
                    else
                    {
                        nextLink = null;
                    }
                } // The jsonDoc is disposed here, but our cloned elements are safe.
            }

            _logger.LogInformation("Successfully fetched a total of {Count} VatGroups from all pages.", allVatGroupElements.Count);

            var finalResult = new { value = allVatGroupElements };
            return JsonSerializer.Serialize(finalResult);
        }

        public async Task CreateVatGroupAsync(TaxDeclaration tax)
        {
            await EnsureLoginAsync();

            string sapCategory = "bovcOutputTax";
            if (tax.TaxCode.Contains("IN", StringComparison.OrdinalIgnoreCase))
            {
                sapCategory = "bovcInputTax";
            }

            var sapVatGroupPayload = new
            {
                Code = tax.TaxCode,
                Name = tax.TaxDescription,
                Category = sapCategory,
                Inactive = tax.IsActive ? "tNO" : "tYES",
                VatGroups_Lines = new[]
                {
            // ==========================================================
            //  THE FINAL FIX IS HERE: Add the Effectivefrom property
            // ==========================================================
            new
            {
                Effectivefrom = tax.ValidFrom, // Use the date from the form
                Rate = tax.TotalPercentage
            }
        }
            };

            var sapJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // Preserve PascalCase
            };

            _logger.LogInformation("Attempting to POST a new VatGroup to SAP with TaxCode: {TaxCode}", tax.TaxCode);

            var response = await _httpClient.PostAsJsonAsync("VatGroups", sapVatGroupPayload, sapJsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create VatGroup in SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Failed to create VatGroup in SAP: {errorContent}");
            }
        }
        public async Task UpdateVatGroupAsync(string taxCode, TaxDeclaration tax)
        {
            await EnsureLoginAsync();
            var sapVatGroupPatch = new
            {
                Name = tax.TaxDescription,
                Inactive = tax.IsActive ? "tNO" : "tYES"
            };

            _logger.LogInformation("Attempting to PATCH VatGroup in SAP: {TaxCode}", taxCode);
            var response = await _httpClient.PatchAsJsonAsync($"VatGroups('{taxCode}')", sapVatGroupPatch);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to update VatGroup in SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Failed to update VatGroup in SAP: {errorContent}");
            }
        }

        public async Task DeleteVatGroupAsync(string taxCode)
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Attempting to DELETE VatGroup from SAP: {TaxCode}", taxCode);
            var response = await _httpClient.DeleteAsync($"VatGroups('{taxCode}')");

            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete VatGroup from SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Failed to delete VatGroup from SAP: {errorContent}");
            }
        }

        public async Task<string> GetWarehousesAsync()
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Attempting to GET all Warehouses from SAP, handling pagination.");

            var allWarehouseElements = new List<JsonElement>();

            // Select only the fields we need to match our Warehouse model
            var fields = "WarehouseCode,WarehouseName,Street";

            // This is the starting URL. It will be replaced by the nextLink on subsequent loops.
            string? nextLink = $"Warehouses?$select={fields}";

            while (!string.IsNullOrEmpty(nextLink))
            {
                _logger.LogInformation("Fetching warehouse page: {PageUrl}", nextLink);
                var response = await _httpClient.GetAsync(nextLink);
                response.EnsureSuccessStatusCode();

                var pageContent = await response.Content.ReadAsStringAsync();

                // The 'using' block ensures the JsonDocument is disposed, which is good practice.
                using (var jsonDoc = JsonDocument.Parse(pageContent))
                {
                    var root = jsonDoc.RootElement;

                    // Add the warehouses from the current page to our master list
                    if (root.TryGetProperty("value", out var valueElement))
                    {
                        // We must CLONE each element. The JsonDocument will be disposed at the
                        // end of the loop, and we need to keep the data.
                        foreach (var element in valueElement.EnumerateArray())
                        {
                            allWarehouseElements.Add(element.Clone());
                        }
                    }

                    // Check if there is a link to the next page of results
                    if (root.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                    {
                        // SAP gives a full URL, but we only need the relative path
                        var fullNextLink = nextLinkElement.GetString();
                        // We use the BaseAddress to get just the part after "v2/"
                        nextLink = _httpClient.BaseAddress != null
                            ? fullNextLink?.Replace(_httpClient.BaseAddress.ToString(), "")
                            : fullNextLink;
                    }
                    else
                    {
                        // No nextLink found, so this is the last page. Exit the loop.
                        nextLink = null;
                    }
                } // The jsonDoc is disposed here, but our cloned elements are safe.
            }

            _logger.LogInformation("Successfully fetched a total of {Count} Warehouses from all pages.", allWarehouseElements.Count);

            // Manually construct the final JSON response object that looks like a single SAP response
            var finalResult = new { value = allWarehouseElements };
            return JsonSerializer.Serialize(finalResult);
        }

        public async Task<string> CreateWarehouseAsync(Warehouse warehouse)
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Attempting to POST a new Warehouse to SAP.");

            // Create the payload SAP expects, mapping our model to SAP's field names
            var sapPayload = new
            {
                WarehouseCode = warehouse.Code,
                WarehouseName = warehouse.Name,
                Street = warehouse.Address
            };

            var content = new StringContent(JsonSerializer.Serialize(sapPayload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("Warehouses", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create Warehouse in SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
            return await response.Content.ReadAsStringAsync();
        }

        public async Task DeleteWarehouseAsync(string warehouseCode)
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Attempting to DELETE Warehouse from SAP: {WarehouseCode}", warehouseCode);

            // The key for Warehouses is the string 'WarehouseCode', which must be in single quotes.
            var requestUrl = $"Warehouses('{warehouseCode}')";

            var response = await _httpClient.DeleteAsync(requestUrl);

            // A successful delete returns 204 No Content. We throw an exception on any other status.
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete Warehouse from SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
        }

        private async Task<int?> GetProductGroupIdAsync(string groupName)
        {
            var groupsJson = await GetProductGroupsAsync();
            using var jsonDoc = JsonDocument.Parse(groupsJson);
            var groups = jsonDoc.RootElement.GetProperty("value");

            foreach (var group in groups.EnumerateArray())
            {
                if (group.TryGetProperty("GroupName", out var nameElement) &&
                    nameElement.GetString()?.Equals(groupName, System.StringComparison.OrdinalIgnoreCase) == true)
                {
                    if (group.TryGetProperty("Number", out var numberElement))
                    {
                        return numberElement.GetInt32();
                    }
                }
            }
            return null;
        }

        // Helper method to find the integer ID for a UOM group name
        private async Task<int?> GetUomGroupIdAsync(string uomGroupName)
        {
            var uomGroups = await GetUomGroupsAsync();
            var group = uomGroups.FirstOrDefault(g => g.Name.Equals(uomGroupName, System.StringComparison.OrdinalIgnoreCase));
            return group?.AbsEntry;
        }

        public async Task<string> GetItemPricesAsync(string itemCode)
        {
            // This method queries the ItemPricesCollection directly for a specific item.
            // The item code must be in single quotes.
            var requestUrl = $"Items('{itemCode}')/ItemPricesCollection";

            var response = await _httpClient.GetAsync(requestUrl);

            // It's okay if this fails (e.g., an item has no prices). We'll handle it.
            if (!response.IsSuccessStatusCode)
            {
                return "{\"value\":[]}"; // Return an empty JSON array on failure
            }

            return await response.Content.ReadAsStringAsync();
        }

        // PASTE THIS ENTIRE METHOD into SapService.cs, replacing the one from the previous step.
        // This is the final and correct version for your SAP environment.

        public async Task<string> GetProductsAsync(string? groupCode, string? searchTerm)
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Fetching Items from SAP with group '{groupCode}' and term '{searchTerm}', selecting ItemPrices directly.", groupCode, searchTerm);

            var allProductElements = new List<JsonElement>();
            var filterClauses = new List<string>();

            if (!string.IsNullOrEmpty(groupCode))
            {
                filterClauses.Add($"ItemsGroupCode eq {groupCode}");
            }
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var sanitizedTerm = searchTerm.Trim().Replace("'", "''");
                var nameFilter = $"substringof('{sanitizedTerm}', ItemName)";
                var codeFilter = $"substringof('{sanitizedTerm}', ItemCode)";
                filterClauses.Add($"({nameFilter} or {codeFilter})");
            }

            // ===================================================================================
            // THE FINAL FIX IS HERE: We remove `$expand` and add `ItemPrices` to the `$select` list.
            // This is the correct syntax for your SAP system.
            // ===================================================================================
            var fields = "ItemCode,ItemName,ItemsGroupCode,InventoryUOM,UoMGroupEntry,U_HS_Code,Picture,ItemPrices";
            string topLimit = string.IsNullOrEmpty(searchTerm) ? "&$top=200" : "";

            // The URL no longer contains an $expand parameter.
            string? nextLink = $"Items?$select={fields}{topLimit}";

            if (filterClauses.Any())
            {
                var filterQuery = string.Join(" and ", filterClauses);
                nextLink += $"&$filter={WebUtility.UrlEncode(filterQuery)}";
            }

            // The rest of the pagination loop remains the same.
            while (!string.IsNullOrEmpty(nextLink))
            {
                _logger.LogInformation("Fetching products page (with prices selected): {PageUrl}", nextLink);
                var response = await _httpClient.GetAsync(nextLink);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await LoginAsync();
                    response = await _httpClient.GetAsync(nextLink);
                }
                response.EnsureSuccessStatusCode(); // This will now pass.

                var pageContent = await response.Content.ReadAsStringAsync();
                using (var jsonDoc = JsonDocument.Parse(pageContent))
                {
                    var root = jsonDoc.RootElement;
                    if (root.TryGetProperty("value", out var valueElement))
                    {
                        foreach (var element in valueElement.EnumerateArray())
                        {
                            allProductElements.Add(element.Clone());
                        }
                    }
                    if (root.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                    {
                        var fullNextLink = nextLinkElement.GetString();
                        nextLink = _httpClient.BaseAddress != null ? fullNextLink?.Replace(_httpClient.BaseAddress.ToString(), "") : fullNextLink;
                    }
                    else
                    {
                        nextLink = null;
                    }
                }
            }

            _logger.LogInformation("Successfully fetched a total of {Count} Products (with prices) from all pages.", allProductElements.Count);
            var finalResult = new { value = allProductElements };
            return JsonSerializer.Serialize(finalResult);
        }

        public async Task<string> CreateProductAsync(ProductCreateDto productDto, string? imageFileName)
        {
            await EnsureLoginAsync();

            // ... (logic for getting itemsGroupCode and uomGroupEntry is unchanged) ...
            int? itemsGroupCode = await GetProductGroupIdAsync(productDto.ProductGroup);
            if (!itemsGroupCode.HasValue) { /*...*/ }
            int? uomGroupEntry = null;
            if (!string.IsNullOrWhiteSpace(productDto.UOMGroup)) { /*...*/ }

            var itemPrices = new List<object>();
            if (decimal.TryParse(productDto.RetailPrice, out var retailPrice))
            {
                itemPrices.Add(new { PriceList = 2, Price = retailPrice }); // Was 1
            }
            if (decimal.TryParse(productDto.WholesalePrice, out var wholesalePrice))
            {
                itemPrices.Add(new { PriceList = 1, Price = wholesalePrice }); // Was 2
            }

            var sapPayload = new
            {
                ItemCode = productDto.SKU,
                ItemName = productDto.ProductName,
                ItemsGroupCode = itemsGroupCode.Value,
                Picture = imageFileName,
                PurchaseItem = "tYES",
                SalesItem = "tYES",
                InventoryItem = "tYES",
                UoMGroupEntry = uomGroupEntry,
                InventoryUOM = productDto.UOM,
                SalesUnit = productDto.UOM,
                PurchaseUnit = productDto.UOM,
                U_HS_Code = productDto.HSN,
                ItemPrices = itemPrices.Any() ? itemPrices : null
            };

            // ... (The rest of the method for sending the request remains the same) ...
            var sapJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };

            var content = new StringContent(JsonSerializer.Serialize(sapPayload, sapJsonOptions), Encoding.UTF8, "application/json");
            // ... rest of the method
            var response = await _httpClient.PostAsync("Items", content);
            // ... error handling and return
            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetProductGroupsAsync()
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Fetching ALL Product Groups (ItemGroups) from SAP, handling pagination.");

            // This list will hold the groups from all pages.
            var allGroupElements = new List<JsonElement>();

            // The initial request URI for the first page.
            string? nextLink = "ItemGroups?$select=Number,GroupName";

            // Loop as long as SAP provides a "nextLink" for the next page.
            while (!string.IsNullOrEmpty(nextLink))
            {
                _logger.LogInformation("Fetching ItemGroups page: {PageUrl}", nextLink);
                var response = await _httpClient.GetAsync(nextLink);
                response.EnsureSuccessStatusCode();

                var pageContent = await response.Content.ReadAsStringAsync();

                // Use 'using' to ensure the JsonDocument is disposed after each loop.
                using (var jsonDoc = JsonDocument.Parse(pageContent))
                {
                    var root = jsonDoc.RootElement;

                    // Get the 'value' array which contains the items for the current page.
                    if (root.TryGetProperty("value", out var valueElement))
                    {
                        // CRITICAL: We must CLONE each element before adding it to our list.
                        // This is because the jsonDoc will be disposed, and we need to keep the data.
                        foreach (var element in valueElement.EnumerateArray())
                        {
                            allGroupElements.Add(element.Clone());
                        }
                    }

                    // Check if the '@odata.nextLink' property exists in the response.
                    if (root.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                    {
                        // It exists, so there are more pages. Get the URL for the next loop iteration.
                        // We extract the relative path from the full URL SAP provides.
                        var fullNextLink = nextLinkElement.GetString();
                        nextLink = _httpClient.BaseAddress != null
                            ? fullNextLink?.Replace(_httpClient.BaseAddress.ToString(), "")
                            : fullNextLink;
                    }
                    else
                    {
                        // No nextLink found. This was the last page. End the loop.
                        nextLink = null;
                    }
                }
            }

            _logger.LogInformation("Successfully fetched a total of {Count} Product Groups from all pages.", allGroupElements.Count);

            // Manually construct a final JSON object that looks like a single, complete SAP response.
            // This ensures that the calling service (ProductGroupService) doesn't need to change.
            var finalResult = new { value = allGroupElements };
            return JsonSerializer.Serialize(finalResult);
        }

        public async Task<string> CreateProductGroupAsync(ProductGroup group)
        {
            await EnsureLoginAsync();

            // SAP expects the property to be "GroupName".
            var sapPayload = new
            {
                GroupName = group.Name
            };

            var sapJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // Preserve PascalCase
            };

            var response = await _httpClient.PostAsJsonAsync("ItemGroups", sapPayload, sapJsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SAP API Error on ItemGroup creation: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Failed to create ItemGroup in SAP. Details: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task DeleteProductGroupAsync(int groupId)
        {
            await EnsureLoginAsync();

            // The key for ItemGroups is the numeric 'Number'.
            var requestUrl = $"ItemGroups({groupId})";
            _logger.LogInformation("Attempting to DELETE ItemGroup from SAP with ID: {Id}", groupId);

            var response = await _httpClient.DeleteAsync(requestUrl);

            // This will fail if the group is in use, which is correct behavior.
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete ItemGroup from SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
        }
        public async Task<List<SapUomResponse>> GetUomsAsync()
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Fetching ALL individual UOMs from SAP, handling pagination.");

            var allUoms = new List<SapUomResponse>();
            // The SAP endpoint for individual units is "UnitOfMeasurements"
            string? requestUri = "UnitOfMeasurements?$select=AbsEntry,Name,Code";

            while (!string.IsNullOrEmpty(requestUri))
            {
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseString);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("value", out var valueElement))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var pageUoms = valueElement.Deserialize<List<SapUomResponse>>(options);
                    if (pageUoms != null)
                    {
                        allUoms.AddRange(pageUoms);
                    }
                }

                if (root.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                {
                    var fullNextLink = nextLinkElement.GetString();
                    requestUri = _httpClient.BaseAddress != null
                        ? fullNextLink?.Replace(_httpClient.BaseAddress.ToString(), "")
                        : fullNextLink;
                }
                else
                {
                    requestUri = null;
                }
            }
            _logger.LogInformation("Successfully fetched a total of {Count} UOMs across all pages.", allUoms.Count);
            return allUoms;
        }

        public async Task<string> CreateUomAsync(UOM uom)
        {
            await EnsureLoginAsync();

            // For a simple UOM, we often use its name as its code.
            var sapPayload = new
            {
                Name = uom.Name,
                Code = uom.Name
            };

            var sapJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // Preserve PascalCase
            };

            var response = await _httpClient.PostAsJsonAsync("UnitOfMeasurements", sapPayload, sapJsonOptions);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SAP API Error on UOM creation: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Failed to create UOM in SAP. Details: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task DeleteUomAsync(int uomId)
        {
            await EnsureLoginAsync();

            // The key for UnitOfMeasurements is the numeric 'AbsEntry'.
            var requestUrl = $"UnitOfMeasurements({uomId})";
            _logger.LogInformation("Attempting to DELETE UOM from SAP with ID: {Id}", uomId);

            var response = await _httpClient.DeleteAsync(requestUrl);

            // Note: This might fail if the UOM is currently in use by an item in SAP.
            // This is expected behavior to protect data integrity.
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete UOM from SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
        }
        /// <summary>
        /// Creates a new Unit of Measure Group in SAP.
        // In your Services/SapService.cs file

        public async Task<List<SapUomResponse>> GetUomGroupsAsync()
        {
            await EnsureLoginAsync();
            _logger.LogInformation("Fetching ALL UOM Groups from SAP, handling pagination.");

            var allGroups = new List<SapUomResponse>();
            string? requestUri = "UnitOfMeasurementGroups?$select=AbsEntry,Code,Name";

            while (!string.IsNullOrEmpty(requestUri))
            {
                var response = await _httpClient.GetAsync(requestUri);
                response.EnsureSuccessStatusCode();

                var responseString = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(responseString);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("value", out var valueElement))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var pageGroups = valueElement.Deserialize<List<SapUomResponse>>(options);
                    if (pageGroups != null)
                    {
                        allGroups.AddRange(pageGroups);
                    }
                }

                if (root.TryGetProperty("@odata.nextLink", out var nextLinkElement))
                {
                    var fullNextLink = nextLinkElement.GetString();
                    requestUri = _httpClient.BaseAddress != null
                        ? fullNextLink?.Replace(_httpClient.BaseAddress.ToString(), "")
                        : fullNextLink;
                }
                else
                {
                    requestUri = null;
                }
            }

            _logger.LogInformation("Successfully fetched a total of {Count} UOM Groups across all pages.", allGroups.Count);
            return allGroups;
        }

        public async Task<string> CreateUomGroupAsync(SapUomGroupCreateDto payload)
        {
            await EnsureLoginAsync();

            var sapJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // Preserves PascalCase
            };

            var content = new StringContent(JsonSerializer.Serialize(payload, sapJsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("UnitOfMeasurementGroups", content);

            if (!response.IsSuccessStatusCode)
            {
                // ** THE FIX IS HERE: Read the content BEFORE logging it. **
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SAP API Error on UOM Group creation: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Failed to create UOM Group in SAP. Details: {errorContent}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<SapUomResponse> CreateUnitOfMeasureAsync(object payload)
        {
            await EnsureLoginAsync();

            var sapJsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null // Preserves PascalCase
            };

            var content = new StringContent(JsonSerializer.Serialize(payload, sapJsonOptions), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("UnitOfMeasurements", content);

            if (!response.IsSuccessStatusCode)
            {
                // ** THE FIX IS HERE: Read the content BEFORE logging it. **
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("SAP API Error on Unit of Measure creation: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                throw new Exception($"Failed to create Unit of Measure in SAP. Details: {errorContent}");
            }

            var responseString = await response.Content.ReadAsStringAsync();
            var deserializeOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<SapUomResponse>(responseString, deserializeOptions);

            if (result == null)
            {
                throw new Exception("Failed to deserialize SAP response for creating Unit of Measure.");
            }

            return result;
        }
        // A simple class to deserialize the response from creating a UoM
        public class SapUomResponse
        {
            public int AbsEntry { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Code { get; set; } = string.Empty;
        }

        public async Task DeleteUomGroupAsync(int groupId)
        {
            await EnsureLoginAsync();

            // The key for UnitOfMeasureGroups is the 'GroupCode'. It must be in parentheses.
            var requestUrl = $"UnitOfMeasureGroups({groupId})";
            _logger.LogInformation("Attempting to DELETE UOM Group from SAP with ID: {Id}", groupId);

            var response = await _httpClient.DeleteAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to delete UOM Group from SAP. Status: {StatusCode}, Response: {ErrorContent}", response.StatusCode, errorContent);
                throw new HttpRequestException(errorContent, null, response.StatusCode);
            }
            // A successful DELETE returns 204 No Content.
        }


        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}