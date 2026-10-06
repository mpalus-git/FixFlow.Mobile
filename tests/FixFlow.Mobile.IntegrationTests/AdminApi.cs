using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace FixFlow.Mobile.IntegrationTests;

public sealed class AdminApi : IDisposable
{
    private readonly HttpClient _client;

    private AdminApi(HttpClient client)
    {
        _client = client;
    }

    public static async Task<AdminApi> LogInAsync(ApiEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        var client = new HttpClient { BaseAddress = environment.BaseAddress };
        var tokens = await PostAsync(client, "api/v1/auth/login", new { email = ApiEnvironment.AdminEmail, password = environment.AdminPassword });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.GetProperty("accessToken").GetString());
        return new AdminApi(client);
    }

    public async Task<Guid> CreateTechnicianAsync(string email, string password) =>
        (await PostAsync(_client, "api/v1/users", new { email, fullName = "Technik Testowy", password, role = "Technician" })).GetProperty("id").GetGuid();

    public async Task<Guid> CreatePartAsync(int stockQuantity) =>
        (await PostAsync(_client, "api/v1/parts", new { name = "Filtr testowy", catalogNumber = $"FT-{Guid.NewGuid():N}", stockQuantity, unitPrice = 12.34m }))
            .GetProperty("id").GetGuid();

    public async Task<Guid> CreateAssignedWorkOrderAsync(Guid technicianId)
    {
        var clientId = (await PostAsync(_client, "api/v1/clients", new
        {
            name = "Klient testowy",
            address = new { street = "Długa", buildingNumber = "1", postalCode = "31-147", city = "Kraków" },
            contactPerson = "Anna Testowa",
            phone = "+48 600 100 200",
        })).GetProperty("id").GetGuid();
        var deviceId = (await PostAsync(_client, "api/v1/devices", new
        {
            clientId,
            serialNumber = $"SN-{Guid.NewGuid():N}",
            model = "Altherma 3",
            manufacturer = "Daikin",
            installationDate = "2021-03-14",
        })).GetProperty("id").GetGuid();
        var workOrderId = (await PostAsync(_client, "api/v1/work-orders", new
        {
            deviceId,
            description = "Przegląd testowy",
            dueDate = DateTimeOffset.UtcNow.AddDays(2).ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
        })).GetProperty("id").GetGuid();
        await PostAsync(_client, $"api/v1/work-orders/{workOrderId}/assign", new { technicianId });
        return workOrderId;
    }

    public async Task ReassignAsync(Guid workOrderId, Guid technicianId) =>
        await PostAsync(_client, $"api/v1/work-orders/{workOrderId}/reassign", new { technicianId });

    public async Task RestockAsync(Guid partId, int quantity) =>
        await PostAsync(_client, $"api/v1/parts/{partId}/restock", new { quantity });

    public async Task<JsonElement> GetAsync(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public void Dispose() => _client.Dispose();

    private static async Task<JsonElement> PostAsync(HttpClient client, string path, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(path, UriKind.Relative), body);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"POST {path} returned {(int)response.StatusCode}: {content}");
        }

        return string.IsNullOrEmpty(content) ? default : JsonDocument.Parse(content).RootElement.Clone();
    }
}
