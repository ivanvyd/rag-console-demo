// ================================================================
// IngestedDocument Entity - Document Metadata and Versioning
// ================================================================
// This class represents metadata for a document that has been processed
// and ingested into the RAG system. It tracks document versions to enable
// incremental processing and maintains source information for document management.
//
// Key Features:
// - Tracks document metadata without storing actual content
// - Enables incremental processing by version comparison
// - Maintains source and document identification
// - Minimal vector dimensions for metadata storage efficiency
// ================================================================

using Microsoft.Extensions.VectorData;

namespace AiMlClarityDemoTerminal.Entities;

/// <summary>
/// Represents metadata for a document that has been ingested into the RAG system.
/// This entity tracks document versions, source information, and processing status
/// without storing the actual document content. The content is stored separately
/// in IngestedChunk entities for efficient retrieval and search operations.
/// </summary>
public class IngestedDocument
{
    // ================================================================
    // Vector Configuration Constants
    // ================================================================
    
    /// <summary>
    /// Minimal vector dimensions for document metadata.
    /// Since this entity doesn't store searchable content, we use the minimum
    /// required dimensions (2) to satisfy the vector store requirements
    /// while optimizing storage space and performance.
    /// </summary>
    private const int VectorDimensions = 2;
    
    /// <summary>
    /// Distance function for vector operations on document metadata.
    /// Although document metadata vectors aren't used for semantic search,
    /// cosine distance provides consistent behavior across the system.
    /// </summary>
    private const string VectorDistanceFunction = DistanceFunction.CosineDistance;

    // ================================================================
    // Entity Properties
    // ================================================================

    /// <summary>
    /// Unique identifier for this document record in the vector database.
    /// Generated using Guid.CreateVersion7() to ensure uniqueness and
    /// provide time-based ordering for better database performance.
    /// </summary>
    [VectorStoreKey]
    public required string Key { get; set; }

    /// <summary>
    /// Identifier of the data source from which this document was ingested.
    /// This typically represents the directory path or data source configuration
    /// that was used to process this document. Enables tracking and management
    /// of documents from multiple sources.
    /// 
    /// Example: "C:\Documents\ProjectFiles" or "LocalPdfDirectorySource:C:\PDFs"
    /// </summary>
    [VectorStoreData(IsIndexed = true)]
    public required string SourceId { get; set; }

    /// <summary>
    /// Unique identifier for the document within its source.
    /// This is typically the filename of the original document and is used
    /// for document identification, citation, and user reference.
    /// 
    /// Example: "ProjectKnowledgeBase.pdf" or "UserManual_v2.pdf"
    /// </summary>
    [VectorStoreData]
    public required string DocumentId { get; set; }

    /// <summary>
    /// Version identifier for the document used for incremental processing.
    /// This is typically based on the file's last modification time and enables
    /// the system to detect when documents have been updated and need reprocessing.
    /// Only changed documents are reprocessed, improving system efficiency.
    /// 
    /// Format: ISO 8601 timestamp (e.g., "2024-01-15T10:30:00.0000000Z")
    /// </summary>
    [VectorStoreData]
    public required string DocumentVersion { get; set; }

    /// <summary>
    /// Minimal vector representation for document metadata.
    /// Since document metadata doesn't require semantic search capabilities,
    /// this vector uses minimal dimensions to satisfy vector store requirements
    /// while optimizing storage efficiency. The actual content vectors are stored
    /// in IngestedChunk entities for semantic search operations.
    /// </summary>
    [VectorStoreVector(VectorDimensions, DistanceFunction = VectorDistanceFunction)]
    public ReadOnlyMemory<float> Vector { get; set; } = new ReadOnlyMemory<float>([0, 0]);
}
