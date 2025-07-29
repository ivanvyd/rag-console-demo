// ================================================================
// IIngestionSource Interface - Data Source Abstraction for RAG System
// ================================================================
// This interface defines the contract for data sources that can provide documents
// to the RAG system for ingestion and processing. It abstracts different types
// of document sources (local files, cloud storage, databases, etc.) behind a
// common interface, enabling flexible and extensible document ingestion.
//
// Key Design Principles:
// - Source-agnostic: Works with any type of document repository
// - Incremental processing: Supports change detection and versioning
// - Efficient operations: Minimizes unnecessary document reprocessing
// - Flexible content extraction: Allows custom document parsing strategies
// ================================================================

using AiMlClarityDemoTerminal.Entities;

namespace AiMlClarityDemoTerminal.Ingestion;

/// <summary>
/// Defines the contract for data sources that provide documents to the RAG ingestion system.
/// Implementations of this interface can connect to various document repositories such as
/// local file systems, cloud storage (Azure Blob, AWS S3), databases, SharePoint, or any
/// other document management system.
/// 
/// The interface is designed to support incremental processing by enabling the detection
/// of new, modified, and deleted documents, making the RAG system efficient for large
/// document collections that change over time.
/// </summary>
public interface IIngestionSource
{
    // ================================================================
    // Source Identification
    // ================================================================

    /// <summary>
    /// Gets a unique identifier for this data source.
    /// This identifier is used to track which documents came from which source,
    /// enabling the system to manage documents from multiple sources simultaneously.
    /// 
    /// Examples:
    /// - "LocalPdfDirectory:C:\Documents\PDFs"
    /// - "AzureBlob:https://storage.blob.core.windows.net/documents"
    /// - "SharePoint:https://company.sharepoint.com/sites/docs"
    /// </summary>
    string SourceId { get; }

    // ================================================================
    // Document Discovery and Change Detection
    // ================================================================

    /// <summary>
    /// Identifies documents that are new or have been modified since the last ingestion.
    /// This method enables incremental processing by comparing the current state of
    /// documents in the source with previously ingested documents.
    /// 
    /// The comparison is typically based on:
    /// - Document version (last modified time, hash, etc.)
    /// - Document existence (new documents not in existing list)
    /// - Content changes (if detectable by the source)
    /// 
    /// Only documents returned by this method will be processed during ingestion,
    /// making the system efficient for regular updates of large document collections.
    /// </summary>
    /// <param name="existingDocuments">
    /// List of documents previously ingested from this source.
    /// Used for comparison to detect changes and new documents.
    /// </param>
    /// <returns>
    /// Collection of documents that need to be processed (new or modified).
    /// Each document includes metadata such as ID, version, and source information.
    /// </returns>
    Task<IEnumerable<IngestedDocument>> GetNewOrModifiedDocumentsAsync(
        IReadOnlyList<IngestedDocument> existingDocuments);

    /// <summary>
    /// Identifies documents that were previously ingested but no longer exist in the source.
    /// This method enables cleanup of deleted documents from the vector database,
    /// ensuring that search results don't include references to non-existent documents.
    /// 
    /// The cleanup process removes:
    /// - Document metadata records
    /// - All associated text chunks and embeddings
    /// - Any cached or derived data
    /// 
    /// This maintains data consistency and prevents users from receiving citations
    /// to documents that are no longer available.
    /// </summary>
    /// <param name="existingDocuments">
    /// List of documents previously ingested from this source.
    /// Used to identify which documents are no longer available.
    /// </param>
    /// <returns>
    /// Collection of documents that no longer exist in the source and should be
    /// removed from the vector database.
    /// </returns>
    Task<IEnumerable<IngestedDocument>> GetDeletedDocumentsAsync(
        IReadOnlyList<IngestedDocument> existingDocuments);

    // ================================================================
    // Content Processing and Extraction
    // ================================================================

    /// <summary>
    /// Processes a document and extracts its content into searchable text chunks.
    /// This method is responsible for:
    /// 
    /// 1. **Content Extraction**: Reading the document content from the source
    ///    - PDF text extraction with layout analysis
    ///    - Office document text extraction
    ///    - Plain text file processing
    ///    - OCR for image-based documents (if supported)
    /// 
    /// 2. **Text Processing**: Cleaning and normalizing extracted text
    ///    - Removing formatting artifacts
    ///    - Handling special characters and encoding
    ///    - Preserving meaningful structure
    /// 
    /// 3. **Semantic Chunking**: Splitting content into optimal-sized pieces
    ///    - Respecting paragraph and section boundaries
    ///    - Maintaining context within chunks
    ///    - Optimizing chunk size for embedding models (typically ~200 tokens)
    ///    - Preserving page numbers for citation purposes
    /// 
    /// 4. **Metadata Preservation**: Maintaining document structure information
    ///    - Page numbers for accurate citations
    ///    - Section headers or document structure
    ///    - Source location within the document
    /// 
    /// The resulting chunks will be automatically converted to vector embeddings
    /// by the ingestion system and stored in the vector database for semantic search.
    /// </summary>
    /// <param name="document">
    /// The document to process, containing metadata such as document ID,
    /// version, and source information.
    /// </param>
    /// <returns>
    /// Collection of text chunks extracted from the document.
    /// Each chunk contains:
    /// - The text content for embedding generation
    /// - Page number for citation purposes
    /// - Document reference for result attribution
    /// - Unique identifier for database storage
    /// </returns>
    Task<IEnumerable<IngestedChunk>> CreateChunksForDocumentAsync(IngestedDocument document);
}
