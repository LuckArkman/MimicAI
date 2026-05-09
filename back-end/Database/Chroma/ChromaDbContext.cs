using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Database.Chroma;

public class ChromaDbContext
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ChromaDbContext(HttpClient httpClient, string baseUrl)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public string BaseUrl => _baseUrl;

    public async Task<bool> CheckHeartbeatAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v2/heartbeat");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> CreateCollectionIfNotExistsAsync(string collectionName)
    {
        try
        {
            var payload = new { name = collectionName, metadata = new { hnsw_space = "cosine" } };
            var content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/api/v2/tenants/default_tenant/databases/default_database/collections", content);
            return response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.Conflict;
        }
        catch
        {
            return false;
        }
    }
}
