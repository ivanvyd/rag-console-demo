// ================================================================
// SemanticSearch Service - Vector-Based Document Retrieval
// ================================================================
// This service provides semantic search capabilities for the RAG system by performing
// vector similarity searches through the ingested document chunks. It converts user
// queries into embeddings and finds the most semantically similar content in the
// vector database, enabling intelligent document retrieval for question answering.
//
// Key Features:
// - Semantic similarity search using vector embeddings
// - Document filtering for targeted searches
// - Configurable result limits for performance optimization
// - Integration with Azure OpenAI embedding models
// - Support for both global and document-specific searches
// ================================================================

using AiMlClarityDemoTerminal.Entities;
using Microsoft.Extensions.VectorData;

namespace AiMlClarityDemoTerminal.Services;

/// <summary>
/// Provides semantic search capabilities for retrieving relevant document chunks
/// based on vector similarity. This service is the core retrieval component of
/// the RAG system, enabling the AI to find contextually relevant information
/// from the ingested document collection.
/// 
/// The search process works by:
/// 1. Converting the search query into a vector embedding
/// 2. Performing similarity search in the vector database
/// 3. Returning the most semantically similar document chunks
/// 4. Preserving document metadata for citation purposes
/// </summary>
/// <param name="vectorCollection">The vector store collection containing document chunks with embeddings</param>
public sealed class SemanticSearch(
    VectorStoreCollection<string, IngestedChunk> vectorCollection)
{
    // ================================================================
    // Core Search Functionality
    // ================================================================

    /// <summary>
    /// Performs semantic search to find document chunks most similar to the given query text.
    /// This method converts the input text to a vector embedding and searches for the most
    /// semantically similar content in the vector database using cosine similarity.
    /// 
    /// The search process:
    /// 1. **Query Embedding**: The input text is automatically converted to a 1536-dimensional
    ///    vector using the same Azure OpenAI embedding model used during ingestion
    /// 
    /// 2. **Similarity Calculation**: The system calculates cosine similarity between the query
    ///    vector and all stored document chunk vectors
    /// 
    /// 3. **Ranking and Filtering**: Results are ranked by similarity score and optionally
    ///    filtered by document ID if specified
    /// 
    /// 4. **Result Retrieval**: The top-k most similar chunks are returned with their
    ///    original text content and metadata for citation
    /// 
    /// This enables the RAG system to find relevant content even when the exact keywords
    /// don't match, as the search operates on semantic meaning rather than literal text matching.
    /// </summary>
    /// <param name="text">
    /// The search query text to find similar content for.
    /// This can be a user question, keyword phrase, or any text that should be
    /// matched semantically against the document collection.
    /// </param>
    /// <param name="documentIdFilter">
    /// Optional filter to limit search results to a specific document.
    /// When provided, only chunks from the specified document will be returned.
    /// Pass null or empty string to search across all documents.
    /// 
    /// Example: "UserManual.pdf" to search only within that document.
    /// </param>
    /// <param name="maxResults">
    /// Maximum number of similar chunks to return.
    /// This controls the breadth of context provided to the AI system.
    /// 
    /// Typical values:
    /// - 3-5 for focused responses with limited context
    /// - 5-10 for comprehensive responses with rich context
    /// - 10+ for research or analysis tasks requiring broad context
    /// 
    /// Higher values provide more context but may include less relevant information
    /// and increase processing time and token usage.
    /// </param>
    /// <returns>
    /// A list of document chunks ordered by semantic similarity to the query.
    /// Each chunk contains:
    /// - **Text**: The original text content for AI processing
    /// - **DocumentId**: Source document name for citation
    /// - **PageNumber**: Page location for precise citation
    /// - **Key**: Unique identifier for the chunk
    /// 
    /// The results are ordered by similarity score (highest first), enabling
    /// the AI to prioritize the most relevant content when generating responses.
    /// </returns>
    public async Task<IReadOnlyList<IngestedChunk>> SearchAsync(
        string text,
        string? documentIdFilter,
        int maxResults)
    {
        // ================================================================
        // Debug: Log Embedding Conversion Process
        // ================================================================
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[DEBUG] Starting embedding conversion for query: \"{text.Substring(0, Math.Min(text.Length, 50))}...\"");
        Console.WriteLine($"[DEBUG] Query text length: {text.Length} characters");
        Console.ResetColor();

        // ================================================================
        // Configure Search Options
        // ================================================================
        
        // Set up vector search configuration with optional document filtering
        VectorSearchOptions<IngestedChunk> options = new()
        {
            // Apply document filter if specified, otherwise search all documents
            Filter = documentIdFilter is { Length: > 0 }
                ? record => record.DocumentId == documentIdFilter  // Filter to specific document
                : null,  // No filter - search all documents
        };

        Console.ForegroundColor = ConsoleColor.DarkGray;
        if (documentIdFilter is { Length: > 0 })
        {
            Console.WriteLine($"[DEBUG] Applying document filter: {documentIdFilter}");
        }
        else
        {
            Console.WriteLine("[DEBUG] Searching across all documents (no filter applied)");
        }
        Console.WriteLine($"[DEBUG] Maximum results requested: {maxResults}");
        Console.ResetColor();

        // ================================================================
        // Execute Vector Similarity Search
        // ================================================================
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine("[DEBUG] Converting query text to 1536-dimensional embedding vector using Azure OpenAI text-embedding-3-small model...");
        var startTime = DateTime.UtcNow;
        Console.ResetColor();

        // Perform the vector search operation:
        // 1. The 'text' parameter is automatically converted to embeddings
        // 2. Cosine similarity is calculated against all stored vectors
        // 3. Results are ranked by similarity score
        // 4. Top 'maxResults' chunks are returned
        var nearest = vectorCollection
            .SearchAsync(text, maxResults, options);

        // ================================================================
        // Process and Return Results
        // ================================================================
        
        // Extract the actual IngestedChunk records from the search results
        // The search returns VectorSearchResult objects that wrap the records
        // with similarity scores and other metadata
        var results = await nearest
            .Select(result => result.Record)  // Extract the IngestedChunk from search result
            .ToListAsync();  // Convert to list for synchronous access

        var endTime = DateTime.UtcNow;
        var searchDuration = endTime - startTime;

        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[DEBUG] Embedding conversion and similarity search completed in {searchDuration.TotalMilliseconds:F2}ms");
        Console.WriteLine($"[DEBUG] Vector similarity search process:");
        Console.WriteLine($"[DEBUG]   1. Query text converted to 1536-dim embedding vector");
        Console.WriteLine($"[DEBUG]   2. Cosine similarity calculated against stored vectors");
        Console.WriteLine($"[DEBUG]   3. Results ranked by similarity score (0.0 to 1.0)");
        Console.WriteLine($"[DEBUG]   4. Top {results.Count} chunks selected from search");
        
        if (results.Count > 0)
        {
            Console.WriteLine($"[DEBUG] Result summary:");
            for (int i = 0; i < Math.Min(3, results.Count); i++)
            {
                var chunk = results[i];
                var preview = chunk.Text.Substring(0, Math.Min(chunk.Text.Length, 80));
                Console.WriteLine($"[DEBUG]   Result {i + 1}: {chunk.DocumentId} (Page {chunk.PageNumber}) - \"{preview}...\"");
            }
            if (results.Count > 3)
            {
                Console.WriteLine($"[DEBUG]   ... and {results.Count - 3} more results");
            }
        }
        
        Console.ResetColor();

        return results;
    }
}
