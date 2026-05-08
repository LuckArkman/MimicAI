namespace Repositorys.Interfaces;

public interface IVectorRepository
{
    Task<VectorSearchResult?> SearchSimilarVectorsAsync(float[] queryEmbedding, int limit = 1);
    Task<bool> InsertVectorAsync(string id, float[] embedding, string document, Dictionary<string, object> metadata);
    Task<bool> EnsureCollectionExistsAsync(string collectionName);
}

public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string DocumentContent { get; set; } = string.Empty;
    public double SimilarityScore { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
