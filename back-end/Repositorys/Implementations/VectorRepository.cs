using System.Net.Http;
using System.Text;
using System.Text.Json;
using Database.Chroma;
using Repositorys.Interfaces;

namespace Repositorys.Implementations;

public class VectorRepository : IVectorRepository
{
    private readonly HttpClient _httpClient;
    private readonly ChromaDbContext _chromaContext;
    private readonly string _collectionName = "mimic_ai_memory";
    private string? _cachedCollectionId;

    public VectorRepository(HttpClient httpClient, ChromaDbContext chromaContext)
    {
        _httpClient = httpClient;
        _chromaContext = chromaContext;
    }

    private async Task<string?> GetCollectionIdAsync()
    {
        if (!string.IsNullOrEmpty(_cachedCollectionId))
            return _cachedCollectionId;

        try
        {
            var response = await _httpClient.GetAsync($"{_chromaContext.BaseUrl}/api/v2/tenants/default_tenant/databases/default_database/collections");
            if (!response.IsSuccessStatusCode) return null;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            
            foreach (var element in doc.RootElement.EnumerateArray())
            {
                if (element.GetProperty("name").GetString() == _collectionName)
                {
                    _cachedCollectionId = element.GetProperty("id").GetString();
                    return _cachedCollectionId;
                }
            }

            // If not found, create it
            bool created = await _chromaContext.CreateCollectionIfNotExistsAsync(_collectionName);
            if (created)
            {
                // Clear cache and call again
                _cachedCollectionId = null;
                return await GetCollectionIdAsync();
            }
        }
        catch
        {
            // Fallback
        }

        return null;
    }

    public async Task<bool> EnsureCollectionExistsAsync(string collectionName)
    {
        return await _chromaContext.CreateCollectionIfNotExistsAsync(collectionName);
    }

    public async Task<VectorSearchResult?> SearchSimilarVectorsAsync(float[] queryEmbedding, int limit = 1)
    {
        string? collectionId = await GetCollectionIdAsync();
        if (string.IsNullOrEmpty(collectionId)) return null;

        try
        {
            var queryPayload = new
            {
                query_embeddings = new[] { queryEmbedding },
                n_results = limit,
                include = new[] { "documents", "metadatas", "distances" }
            };

            var content = new StringContent(JsonSerializer.Serialize(queryPayload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_chromaContext.BaseUrl}/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/query", content);
            
            if (!response.IsSuccessStatusCode) return null;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonString);
            var root = doc.RootElement;

            // Extract results from arrays
            var ids = root.GetProperty("ids").GetProperty("0");
            var distances = root.GetProperty("distances").GetProperty("0");
            var documents = root.GetProperty("documents").GetProperty("0");
            var metadatas = root.GetProperty("metadatas").GetProperty("0");

            if (ids.GetArrayLength() == 0) return null;

            // In ChromaDB, distance can be converted to similarity score (for cosine: distance = 1 - similarity_score)
            // So similarity_score = 1 - distance. Let's calculate.
            double distance = distances[0].GetDouble();
            double similarity = 1.0 - distance;

            var result = new VectorSearchResult
            {
                Id = ids[0].GetString() ?? string.Empty,
                DocumentContent = documents[0].GetString() ?? string.Empty,
                SimilarityScore = similarity
            };

            var metaObj = metadatas[0];
            foreach (var prop in metaObj.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    result.Metadata[prop.Name] = prop.Value.GetString() ?? string.Empty;
                else if (prop.Value.ValueKind == JsonValueKind.Number)
                    result.Metadata[prop.Name] = prop.Value.GetDouble();
                else if (prop.Value.ValueKind == JsonValueKind.True || prop.Value.ValueKind == JsonValueKind.False)
                    result.Metadata[prop.Name] = prop.Value.GetBoolean();
            }

            return result;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> InsertVectorAsync(string id, float[] embedding, string document, Dictionary<string, object> metadata)
    {
        string? collectionId = await GetCollectionIdAsync();
        if (string.IsNullOrEmpty(collectionId)) return false;

        try
        {
            var addPayload = new
            {
                ids = new[] { id },
                embeddings = new[] { embedding },
                documents = new[] { document },
                metadatas = new[] { metadata }
            };

            var content = new StringContent(JsonSerializer.Serialize(addPayload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_chromaContext.BaseUrl}/api/v2/tenants/default_tenant/databases/default_database/collections/{collectionId}/add", content);
            
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
