// ================================================================
// DataIngestor - Document Processing and Vector Storage Orchestrator
// ================================================================
// This service orchestrates the complete document ingestion pipeline for the RAG system.
// It manages the processing of documents from various sources, handles incremental updates,
// and coordinates storage of both document metadata and text chunks in the vector database.
//
// Key Responsibilities:
// - Coordinate document ingestion from multiple sources
// - Implement incremental processing to avoid reprocessing unchanged documents
// - Manage vector database operations for documents and chunks
// - Handle cleanup of deleted or modified documents
// - Provide detailed logging and error handling for debugging
// ================================================================

using AiMlClarityDemoTerminal.Entities;
using Microsoft.Extensions.VectorData;

namespace AiMlClarityDemoTerminal.Ingestion;

/// <summary>
/// Orchestrates the document ingestion pipeline for the RAG system.
/// This service processes documents from various sources, converts them into searchable chunks,
/// generates vector embeddings, and stores everything in the vector database for semantic search.
/// 
/// The ingestion process is incremental - only new or modified documents are processed,
/// making the system efficient for large document collections.
/// </summary>
/// <param name="chunksCollection">Vector store collection for text chunks with embeddings</param>
/// <param name="documentsCollection">Vector store collection for document metadata</param>
public sealed class DataIngestor(
    VectorStoreCollection<string, IngestedChunk> chunksCollection,
    VectorStoreCollection<string, IngestedDocument> documentsCollection)
{
    // ================================================================
    // Main Ingestion Pipeline
    // ================================================================

    /// <summary>
    /// Processes documents from the specified source and stores them in the vector database.
    /// This method implements a complete RAG ingestion pipeline:
    /// 
    /// 1. Sets up vector database collections
    /// 2. Identifies new, modified, and deleted documents
    /// 3. Processes document content into searchable chunks
    /// 4. Generates vector embeddings for semantic search
    /// 5. Stores everything in the vector database
    /// 
    /// The process is incremental - only documents that have changed since the last
    /// ingestion are processed, making it efficient for regular updates.
    /// </summary>
    /// <param name="source">The data source providing documents to ingest</param>
    /// <returns>A task representing the asynchronous ingestion operation</returns>
    public async Task IngestDataAsync(IIngestionSource source)
    {
        try
        {
            // ================================================================
            // Phase 1: Initialize Vector Database Collections
            // ================================================================
            Console.WriteLine("[SETUP] Setting up vector collections...");
            
            // Ensure the vector database tables exist and are properly configured
            // This creates the necessary schema for storing documents and chunks with embeddings
            await chunksCollection.EnsureCollectionExistsAsync();
            await documentsCollection.EnsureCollectionExistsAsync();
            
            Console.WriteLine("[READY] Vector collections ready");

            // ================================================================
            // Phase 2: Discover Existing Documents
            // ================================================================
            var sourceId = source.SourceId;
            Console.WriteLine($"[SOURCE] Processing source: {sourceId}");
            
            Console.WriteLine("[CHECK] Checking existing documents...");
            
            // Retrieve all documents previously ingested from this source
            // This enables incremental processing by comparing versions
            var documentsForSource = await documentsCollection
                .GetAsync(doc => doc.SourceId == sourceId, top: int.MaxValue)
                .ToListAsync();
            
            Console.WriteLine($"[COUNT] Found {documentsForSource.Count} existing documents");

            // ================================================================
            // Phase 3: Handle Deleted Documents
            // ================================================================
            Console.WriteLine("[DELETE] Checking for deleted documents...");
            
            // Identify documents that were previously ingested but no longer exist in the source
            var deletedDocuments = await source
                .GetDeletedDocumentsAsync(documentsForSource);

            // Clean up deleted documents and their associated chunks
            foreach (var deletedDocument in deletedDocuments)
            {
                Console.WriteLine($"[REMOVE] Deleting document: {deletedDocument.DocumentId}");
                
                // Remove all text chunks associated with this document
                await DeleteChunksForDocumentAsync(deletedDocument);
                
                // Remove the document metadata record
                await documentsCollection.DeleteAsync(deletedDocument.Key);
            }

            // ================================================================
            // Phase 4: Process New and Modified Documents
            // ================================================================
            Console.WriteLine("[SCAN] Checking for new or modified documents...");
            
            // Identify documents that are new or have been modified since last ingestion
            // This comparison is based on document version (typically last modified time)
            var modifiedDocuments = await source
                .GetNewOrModifiedDocumentsAsync(documentsForSource);
            
            Console.WriteLine($"[PROCESS] Found {modifiedDocuments.Count()} documents to process");

            // Process each new or modified document
            foreach (var modifiedDocument in modifiedDocuments)
            {
                Console.WriteLine($"[DOC] Processing document: {modifiedDocument.DocumentId}");
                
                try
                {
                    // ================================================================
                    // Phase 4a: Clean Up Existing Content
                    // ================================================================
                    Console.WriteLine($"[CLEAN] Cleaning up existing chunks for: {modifiedDocument.DocumentId}");
                    
                    // Remove any existing chunks for this document to avoid duplicates
                    // This ensures that modified documents are completely reprocessed
                    await DeleteChunksForDocumentAsync(modifiedDocument);

                    // ================================================================
                    // Phase 4b: Store Document Metadata
                    // ================================================================
                    Console.WriteLine($"[SAVE] Saving document metadata: {modifiedDocument.DocumentId}");
                    
                    // Store or update the document metadata record
                    // This tracks the document version and source information
                    await documentsCollection.UpsertAsync(modifiedDocument);

                    // ================================================================
                    // Phase 4c: Process Document Content
                    // ================================================================
                    Console.WriteLine($"[CHUNK] Creating text chunks for: {modifiedDocument.DocumentId}");
                    
                    // Extract text content and split into semantic chunks
                    // This process involves PDF parsing, text extraction, and intelligent chunking
                    var newRecords = await source
                        .CreateChunksForDocumentAsync(modifiedDocument);
                    
                    var newRecordsList = newRecords.ToList();
                    Console.WriteLine($"[CREATED] Created {newRecordsList.Count} chunks for: {modifiedDocument.DocumentId}");

                    // ================================================================
                    // Phase 4d: Store Vector Embeddings
                    // ================================================================
                    if (newRecordsList.Count > 0)
                    {
                        Console.WriteLine($"[STORE] Saving chunks to vector store for: {modifiedDocument.DocumentId}");
                        
                        // Debug: Log embedding generation process
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"[DEBUG] Starting embedding generation for {newRecordsList.Count} text chunks");
                        Console.WriteLine($"[DEBUG] Each chunk will be converted to 1536-dimensional embedding vector");
                        Console.WriteLine($"[DEBUG] Using Azure OpenAI text-embedding-3-small model");
                        var embeddingStartTime = DateTime.UtcNow;
                        Console.ResetColor();

                        // Log sample chunks being processed
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"[DEBUG] Sample chunks being processed:");
                        for (int i = 0; i < Math.Min(3, newRecordsList.Count); i++)
                        {
                            var chunk = newRecordsList[i];
                            var preview = chunk.Text.Substring(0, Math.Min(chunk.Text.Length, 100));
                            Console.WriteLine($"[DEBUG]   Chunk {i + 1}: Page {chunk.PageNumber} - \"{preview}...\"");
                            Console.WriteLine($"[DEBUG]   Text length: {chunk.Text.Length} characters");
                        }
                        if (newRecordsList.Count > 3)
                        {
                            Console.WriteLine($"[DEBUG]   ... and {newRecordsList.Count - 3} more chunks");
                        }
                        Console.ResetColor();
                        
                        // Store all chunks in the vector database
                        // This automatically generates embeddings for each chunk's text content
                        await chunksCollection.UpsertAsync(newRecordsList);
                        
                        var embeddingEndTime = DateTime.UtcNow;
                        var embeddingDuration = embeddingEndTime - embeddingStartTime;
                        
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"[DEBUG] Embedding generation completed in {embeddingDuration.TotalSeconds:F2} seconds");
                        Console.WriteLine($"[DEBUG] Average time per chunk: {embeddingDuration.TotalMilliseconds / newRecordsList.Count:F2}ms");
                        Console.WriteLine($"[DEBUG] Total vectors created: {newRecordsList.Count} x 1536 dimensions = {newRecordsList.Count * 1536:N0} float values");
                        Console.WriteLine($"[DEBUG] Estimated storage size: ~{newRecordsList.Count * 1536 * 4 / 1024:F1} KB for embeddings");
                        Console.WriteLine($"[DEBUG] Vector storage process:");
                        Console.WriteLine($"[DEBUG]   1. Text chunks sent to Azure OpenAI embedding API");
                        Console.WriteLine($"[DEBUG]   2. Each text converted to 1536-dimensional float array");
                        Console.WriteLine($"[DEBUG]   3. Vectors stored in SQLite with vector extension");
                        Console.WriteLine($"[DEBUG]   4. Cosine similarity index created for fast search");
                        Console.ResetColor();
                        
                        Console.WriteLine($"[SUCCESS] Successfully processed: {modifiedDocument.DocumentId}");
                    }
                }
                catch (Exception ex)
                {
                    // Handle document-specific errors while continuing with other documents
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[ERROR] Error processing document {modifiedDocument.DocumentId}: {ex.Message}");
                    Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                    Console.ResetColor();
                    throw; // Re-throw to stop processing on document errors
                }
            }
        }
        catch (Exception ex)
        {
            // Handle fatal errors that affect the entire ingestion process
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FATAL] Fatal error during ingestion: {ex.Message}");
            Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            Console.ResetColor();
            throw; // Re-throw for upstream error handling
        }

        // ================================================================
        // Helper Method: Document Cleanup
        // ================================================================

        /// <summary>
        /// Removes all text chunks associated with a specific document from the vector database.
        /// This is used when documents are deleted or modified to ensure data consistency
        /// and prevent duplicate or stale content in search results.
        /// </summary>
        /// <param name="document">The document whose chunks should be deleted</param>
        /// <returns>A task representing the asynchronous cleanup operation</returns>
        async Task DeleteChunksForDocumentAsync(IngestedDocument document)
        {
            try
            {
                var documentId = document.DocumentId;
                
                // Find all chunks belonging to this document
                var chunksToDelete = await chunksCollection
                    .GetAsync(record => record.DocumentId == documentId, int.MaxValue)
                    .ToListAsync();

                if (chunksToDelete.Count != 0)
                {
                    Console.WriteLine($"[DELETE] Deleting {chunksToDelete.Count} chunks for document: {documentId}");
                    
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[DEBUG] Removing {chunksToDelete.Count} embedding vectors from database");
                    Console.WriteLine($"[DEBUG] Freeing ~{chunksToDelete.Count * 1536 * 4 / 1024:F1} KB of vector storage");
                    Console.ResetColor();
                    
                    // Extract the keys for bulk deletion
                    var chunksToDeleteKeys = chunksToDelete
                        .Select(r => r.Key);

                    // Perform bulk deletion for efficiency
                    await chunksCollection.DeleteAsync(chunksToDeleteKeys);
                    
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[DEBUG] Successfully removed {chunksToDelete.Count} vectors from database");
                    Console.ResetColor();
                }
            }
            catch (Exception ex)
            {
                // Handle cleanup errors separately to provide specific error context
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ERROR] Error deleting chunks for document {document.DocumentId}: {ex.Message}");
                Console.ResetColor();
                throw; // Re-throw to maintain error propagation
            }
        }
    }
}
