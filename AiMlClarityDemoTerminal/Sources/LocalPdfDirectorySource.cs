// ================================================================
// LocalPdfDirectorySource - Local PDF File Processing Implementation
// ================================================================
// This class implements the IIngestionSource interface to process PDF documents
// from a local directory. It provides comprehensive PDF text extraction, document
// layout analysis, and intelligent text chunking for optimal RAG performance.
//
// Key Features:
// - Advanced PDF text extraction using document layout analysis
// - Intelligent text chunking with semantic boundaries
// - Incremental processing based on file modification times
// - Comprehensive error handling and logging
// - Page-aware text extraction for accurate citations
// - Optimized chunk sizes for embedding models
// ================================================================

using AiMlClarityDemoTerminal.Entities;
using AiMlClarityDemoTerminal.Ingestion;
using Microsoft.SemanticKernel.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace AiMlClarityDemoTerminal.Sources;

/// <summary>
/// Implements document ingestion from a local directory containing PDF files.
/// This source processes PDF documents using advanced text extraction and layout analysis
/// to produce high-quality text chunks optimized for semantic search and retrieval.
/// 
/// The implementation provides:
/// - **Incremental Processing**: Only processes new or modified files
/// - **Advanced PDF Parsing**: Uses document layout analysis for better text extraction
/// - **Semantic Chunking**: Splits content into meaningful segments
/// - **Page Tracking**: Maintains page numbers for accurate citations
/// - **Error Resilience**: Comprehensive error handling for robust operation
/// </summary>
/// <param name="sourceDirectory">The local directory path containing PDF files to process</param>
public class LocalPdfDirectorySource(string sourceDirectory)
    : IIngestionSource
{
    // ================================================================
    // Supporting Data Structures
    // ================================================================

    /// <summary>
    /// Represents a text paragraph extracted from a PDF page with its location metadata.
    /// This record preserves the relationship between text content and its source location
    /// for accurate citation and reference purposes.
    /// </summary>
    /// <param name="PageNumber">The page number where this paragraph was found</param>
    /// <param name="IndexOnPage">The sequential index of this paragraph on the page</param>
    /// <param name="Text">The extracted text content of the paragraph</param>
    private record Paragraph(int PageNumber, int IndexOnPage, string Text);

    // ================================================================
    // Document Identification Utilities
    // ================================================================

    /// <summary>
    /// Extracts a unique document identifier from the file path.
    /// Uses the filename as the document ID for user-friendly citations.
    /// </summary>
    /// <param name="path">Full file path to the PDF document</param>
    /// <returns>Filename that serves as the document identifier</returns>
    public static string SourceFileId(string path) =>
        Path.GetFileName(path);

    /// <summary>
    /// Generates a version identifier for a document based on its last modification time.
    /// This enables incremental processing by detecting when files have changed.
    /// </summary>
    /// <param name="path">Full file path to the PDF document</param>
    /// <returns>ISO 8601 formatted timestamp representing the document version</returns>
    public static string SourceFileVersion(string path) =>
        File.GetLastWriteTimeUtc(path).ToString("o");

    /// <summary>
    /// Gets the unique identifier for this data source instance.
    /// Uses the source directory path to distinguish between different PDF collections.
    /// </summary>
    public string SourceId => sourceDirectory;

    // ================================================================
    // Document Discovery and Change Detection
    // ================================================================

    /// <summary>
    /// Scans the source directory for PDF files and identifies which ones need processing.
    /// This method implements incremental processing by comparing file modification times
    /// with previously ingested document versions.
    /// 
    /// The process:
    /// 1. **Directory Scanning**: Finds all PDF files in the source directory
    /// 2. **Version Comparison**: Compares current file versions with existing records
    /// 3. **Change Detection**: Identifies new files and files modified since last ingestion
    /// 4. **Metadata Creation**: Generates document metadata for processing pipeline
    /// 
    /// Only documents that are new or have been modified will be returned for processing,
    /// making the system efficient for regular updates of large PDF collections.
    /// </summary>
    /// <param name="existingDocuments">Previously ingested documents from this source</param>
    /// <returns>Collection of documents that need to be processed</returns>
    public Task<IEnumerable<IngestedDocument>> GetNewOrModifiedDocumentsAsync(
        IReadOnlyList<IngestedDocument> existingDocuments)
    {
        try
        {
            Console.WriteLine($"[SCAN] Scanning directory: {sourceDirectory}");
            
            // Verify source directory exists before processing
            if (!Directory.Exists(sourceDirectory))
            {
                Console.WriteLine($"[WARNING] Directory does not exist: {sourceDirectory}");
                return Task.FromResult(Enumerable.Empty<IngestedDocument>());
            }

            List<IngestedDocument> results = [];

            // Find all PDF files in the source directory
            string[] sourceFiles = Directory.GetFiles(
                sourceDirectory, 
                "*.pdf");
            
            Console.WriteLine($"[FILES] Found {sourceFiles.Length} PDF files");

            // Create lookup dictionary for efficient version comparison
            Dictionary<string, IngestedDocument> existingDocumentsById = existingDocuments
                .ToDictionary(d => d.DocumentId);

            // Process each PDF file to determine if it needs ingestion
            foreach (var sourceFile in sourceFiles)
            {
                string sourceFileId = SourceFileId(sourceFile);
                string sourceFileVersion = SourceFileVersion(sourceFile);
                
                // Check if this document was previously ingested and get its version
                string? existingDocumentVersion = existingDocumentsById
                    .TryGetValue(sourceFileId, out var existingDocument)
                        ? existingDocument.DocumentVersion
                        : null;

                // Document needs processing if it's new or has been modified
                if (existingDocumentVersion != sourceFileVersion)
                {
                    Console.WriteLine($"[NEW] Document needs processing: {sourceFileId}");
                    results.Add(new IngestedDocument
                    {
                        Key = Guid.CreateVersion7().ToString(),  // Time-ordered unique identifier
                        SourceId = SourceId,                     // Source directory path
                        DocumentId = sourceFileId,               // Filename for user reference
                        DocumentVersion = sourceFileVersion     // Modification timestamp
                    });
                }
                else
                {
                    Console.WriteLine($"[SKIP] Document already up to date: {sourceFileId}");
                }
            }

            Console.WriteLine($"[TOTAL] Total documents to process: {results.Count}");
            return Task.FromResult((IEnumerable<IngestedDocument>)results);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Error in GetNewOrModifiedDocumentsAsync: {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }

    /// <summary>
    /// Identifies documents that were previously ingested but are no longer present in the source directory.
    /// This enables cleanup of deleted files from the vector database to maintain data consistency.
    /// </summary>
    /// <param name="existingDocuments">Previously ingested documents from this source</param>
    /// <returns>Collection of documents that no longer exist and should be removed</returns>
    public Task<IEnumerable<IngestedDocument>> GetDeletedDocumentsAsync(
        IReadOnlyList<IngestedDocument> existingDocuments)
    {
        try
        {
            // Get current files in the source directory
            string[] currentFiles = Directory.GetFiles(
                sourceDirectory, 
                "*.pdf");

            // Create lookup for quick file existence checking
            ILookup<string, string> currentFileIds = currentFiles
                .ToLookup(SourceFileId);

            // Find documents that were previously ingested but no longer exist
            IEnumerable<IngestedDocument> deletedDocuments = existingDocuments
                .Where(d => !currentFileIds.Contains(d.DocumentId));

            var deletedList = deletedDocuments.ToList();
            if (deletedList.Count > 0)
            {
                Console.WriteLine($"[DELETED] Found {deletedList.Count} deleted documents");
            }

            return Task.FromResult((IEnumerable<IngestedDocument>)deletedList);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Error in GetDeletedDocumentsAsync: {ex.Message}");
            Console.ResetColor();
            throw;
        }
    }

    // ================================================================
    // PDF Content Processing and Text Extraction
    // ================================================================

    /// <summary>
    /// Processes a PDF document and extracts its content into searchable text chunks.
    /// This method implements a sophisticated PDF processing pipeline:
    /// 
    /// 1. **PDF Opening**: Safely opens the PDF file with error handling
    /// 2. **Layout Analysis**: Uses advanced document layout analysis to understand structure
    /// 3. **Text Extraction**: Extracts text while preserving logical flow and formatting
    /// 4. **Semantic Chunking**: Splits content into optimal-sized chunks for embeddings
    /// 5. **Metadata Preservation**: Maintains page numbers and document references
    /// 
    /// The resulting chunks are optimized for:
    /// - Semantic coherence (respecting paragraph boundaries)
    /// - Embedding model efficiency (~200 tokens per chunk)
    /// - Citation accuracy (page number tracking)
    /// - Search relevance (meaningful content segments)
    /// </summary>
    /// <param name="document">Document metadata containing file information</param>
    /// <returns>Collection of text chunks ready for vector embedding</returns>
    public Task<IEnumerable<IngestedChunk>> CreateChunksForDocumentAsync(IngestedDocument document)
    {
        try
        {
            // ================================================================
            // Phase 1: PDF File Access and Validation
            // ================================================================
            string pdfPath = Path.Combine(sourceDirectory, document.DocumentId);
            Console.WriteLine($"[OPEN] Opening PDF: {pdfPath}");

            if (!File.Exists(pdfPath))
            {
                throw new FileNotFoundException($"PDF file not found: {pdfPath}");
            }

            // ================================================================
            // Phase 2: PDF Content Extraction
            // ================================================================
            Console.WriteLine($"[PROCESS] Processing PDF content...");
            
            // Open PDF document using PdfPig library for advanced text extraction
            using PdfDocument pdf = PdfDocument.Open(pdfPath);
            Console.WriteLine($"[PAGES] PDF has {pdf.NumberOfPages} pages");

            Console.WriteLine($"[EXTRACT] Extracting text from pages...");
            
            // Process each page and extract paragraphs with layout analysis
            // This preserves the logical structure of the document content
            List<Paragraph> paragraphs = [.. pdf
                .GetPages()
                .SelectMany(GetPageParagraphs)];
            
            Console.WriteLine($"[PARAGRAPHS] Extracted {paragraphs.Count} paragraphs");

            // ================================================================
            // Phase 3: Convert to IngestedChunk Entities
            // ================================================================
            
            // Transform processed paragraphs into IngestedChunk entities
            // Each chunk includes the text content and metadata for vector storage
            var chunks = paragraphs.Select(p => new IngestedChunk
            {
                Key = Guid.CreateVersion7().ToString(),  // Unique identifier
                DocumentId = document.DocumentId,         // Source document reference
                PageNumber = p.PageNumber,                // Page location for citations
                Text = p.Text,                           // Text content for embedding
            }).ToList();

            Console.WriteLine($"[CHUNKS] Created {chunks.Count} text chunks");
            return Task.FromResult((IEnumerable<IngestedChunk>)chunks);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Error processing PDF {document.DocumentId}: {ex.Message}");
            Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
            Console.ResetColor();
            throw;
        }
    }

    // ================================================================
    // Advanced PDF Text Processing
    // ================================================================

    /// <summary>
    /// Extracts and processes text from a single PDF page using advanced document layout analysis.
    /// This method implements a sophisticated text extraction pipeline:
    /// 
    /// 1. **Letter Detection**: Identifies individual characters and their positions
    /// 2. **Word Formation**: Groups letters into words using spatial analysis
    /// 3. **Block Detection**: Identifies text blocks and their logical relationships
    /// 4. **Content Assembly**: Combines blocks into coherent text with proper spacing
    /// 5. **Semantic Chunking**: Splits large text into embedding-optimized segments
    /// 
    /// The process preserves document structure while optimizing content for semantic search.
    /// </summary>
    /// <param name="pdfPage">The PDF page to process</param>
    /// <returns>Collection of text paragraphs with page and position information</returns>
    private static IEnumerable<Paragraph> GetPageParagraphs(Page pdfPage)
    {
        try
        {
            Console.WriteLine($"[PAGE] Processing page {pdfPage.Number}...");
            
            // ================================================================
            // Step 1: Extract Letters and Characters
            // ================================================================
            var letters = pdfPage.Letters;
            Console.WriteLine($"[LETTERS] Found {letters.Count} letters on page {pdfPage.Number}");
            
            // ================================================================
            // Step 2: Word Formation Using Spatial Analysis
            // ================================================================
            // Use nearest neighbor algorithm to group letters into words
            // This handles complex layouts and preserves reading order
            var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);
            var wordsList = words.ToList();
            Console.WriteLine($"[WORDS] Extracted {wordsList.Count} words from page {pdfPage.Number}");
            
            // ================================================================
            // Step 3: Text Block Detection and Organization
            // ================================================================
            // Use Docstrum algorithm to identify text blocks and their relationships
            // This maintains logical flow and paragraph structure
            var textBlocks = DocstrumBoundingBoxes.Instance.GetBlocks(wordsList);
            var textBlocksList = textBlocks.ToList();
            Console.WriteLine($"[BLOCKS] Created {textBlocksList.Count} text blocks from page {pdfPage.Number}");
            
            // ================================================================
            // Step 4: Text Assembly and Normalization
            // ================================================================
            // Combine text blocks into coherent content with proper spacing
            // Replace line endings with spaces to create flowing text
            var pageText = string.Join(Environment.NewLine + Environment.NewLine,
                textBlocksList.Select(t => t.Text.ReplaceLineEndings(" ")));

            // Handle pages with no extractable text
            if (string.IsNullOrWhiteSpace(pageText))
            {
                Console.WriteLine($"[WARNING] No text extracted from page {pdfPage.Number}");
                return Enumerable.Empty<Paragraph>();
            }

            // ================================================================
            // Step 5: Semantic Chunking for Optimal Embedding
            // ================================================================
            Console.WriteLine($"[SPLIT] Splitting page {pdfPage.Number} text into chunks...");

            // Use Semantic Kernel's text chunker to create optimal-sized chunks
            // 200 tokens is optimal for most embedding models and provides good context
#pragma warning disable SKEXP0050 // Type is for evaluation purposes only
            var paragraphs = TextChunker.SplitPlainTextParagraphs([pageText], 200)
                .Select((text, index) =>
                    new Paragraph(pdfPage.Number, index, text))
                .ToList();
#pragma warning restore SKEXP0050 // Type is for evaluation purposes only

            Console.WriteLine($"[RESULT] Created {paragraphs.Count} paragraphs from page {pdfPage.Number}");
            return paragraphs;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ERROR] Error processing page {pdfPage.Number}: {ex.Message}");
            Console.ResetColor();
            return Enumerable.Empty<Paragraph>();
        }
    }
}
