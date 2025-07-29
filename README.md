# AI/ML Clarity Demo - Terminal RAG Application

A comprehensive console application demonstrating **Retrieval-Augmented Generation (RAG)** using Azure OpenAI, local PDF document processing, and vector-based semantic search.

## Overview

This terminal application showcases how to build a production-ready RAG system that:
- Processes PDF documents and extracts text content
- Creates semantic embeddings using Azure OpenAI
- Stores documents in a local SQLite vector database
- Provides interactive chat with AI assistant
- Automatically searches through documents to answer questions

## Architecture

### Core Components

| Component | Purpose | Technology |
|-----------|---------|------------|
| **Document Ingestion** | PDF processing and text extraction | PdfPig library |
| **Vector Storage** | Semantic search and embeddings | SQLite with vector extensions |
| **AI Services** | Chat completion and embeddings | Azure OpenAI (LLM, text-embedding-3-small) |
| **Search Engine** | Semantic document retrieval | Microsoft.Extensions.VectorData |
| **Function Calling** | Automatic tool invocation by AI | Microsoft.Extensions.AI |

### Project Structure

```
AiMlClarityDemoTerminal/
├── Program.cs                     # Main application entry point and RAG orchestration
├── Data/                          # Directory for PDF files to be processed
├── Entities/                      # Data models for documents and chunks
│   ├── IngestedDocument.cs        # Metadata for processed documents
│   └── IngestedChunk.cs          # Text chunks with embeddings
├── Ingestion/                     # Document processing pipeline
│   ├── DataIngestor.cs           # Orchestrates document ingestion
│   └── IIngestionSource.cs       # Interface for data sources
├── Services/                      # Core business logic
│   └── SemanticSearch.cs         # Vector-based document search
└── Sources/                       # Data source implementations
    └── LocalPdfDirectorySource.cs # Local PDF file processing
```

## Features

### 1. **Intelligent Document Processing**
- Automatically scans PDF files in the `Data` directory
- Extracts text using advanced document layout analysis
- Splits content into semantically meaningful chunks
- Tracks document versions to avoid reprocessing

### 2. **Vector-Based Semantic Search**
- Converts text chunks into high-dimensional embeddings
- Stores embeddings in local SQLite vector database
- Performs similarity search to find relevant content
- Supports filtering by document or content type

### 3. **Interactive AI Chat**
- Streaming chat responses for real-time interaction
- Automatic function calling for document search
- Context-aware conversations with memory
- Citations with document names and page numbers

### 4. **Production-Ready Features**
- Comprehensive error handling and logging
- Timeout protection against hanging operations
- Incremental document processing (only new/changed files)
- Colored console output for better UX

### 5. **Debug and Technical Insights**
- Detailed embedding conversion process logging
- Vector similarity search technical details
- Performance metrics and timing information
- Storage space calculations and optimization data

## Dependencies

### Core AI Libraries
- **Azure.AI.OpenAI** (2.2.0-beta.5) - Azure OpenAI service integration
- **Microsoft.Extensions.AI** (9.7.1) - Unified AI abstractions and function calling
- **Microsoft.Extensions.AI.OpenAI** (9.7.1-preview) - OpenAI-specific implementations

### Document Processing
- **PdfPig** (0.1.10) - Pure .NET PDF text extraction
- **Microsoft.SemanticKernel.Core** (1.53.0) - Text chunking and processing

### Vector Database
- **Microsoft.SemanticKernel.Connectors.SqliteVec** (1.53.0-preview) - Local vector storage

### Supporting Libraries
- **Azure.Identity** (1.14.0) - Azure authentication
- **System.Linq.Async** (6.0.1) - Asynchronous LINQ operations

## Prerequisites

1. **.NET 9 SDK** - Latest .NET runtime and development tools
2. **Azure OpenAI Service** - Access to LLM and text-embedding-3-small models
3. **PDF Documents** - Sample documents to demonstrate RAG capabilities

## Usage

### 1. Setup
```bash
# Clone the repository
git clone <repository-url>
cd AiMlClarityDemoTerminal

# Restore dependencies
dotnet restore

# Add your PDF files to the Data directory
cp your-documents.pdf Data/
```

### 2. Configuration
Update the Azure OpenAI configuration in `Program.cs`:
```csharp
string endpoint = "https://your-resource.openai.azure.com/";
string apiKey = "your-api-key";
```

### 3. Run the Application
```bash
dotnet run
```

### 4. Interact with the AI
```
=== AI/ML Clarity Demo - RAG Console Chat ===
[INFO] I can search through your PDF documents to answer questions.
[USER] You: What is this document about?
[AI] Assistant: Based on the document content, this appears to be...
```

## Sample Queries

Try these example questions to see the RAG system in action:

- **Document Overview**: "What is this document about?"
- **Content Summary**: "Summarize the main points"
- **Specific Topics**: "Tell me about [specific concept]"
- **Data Extraction**: "What are the key findings?"
- **Comparative Analysis**: "How does this relate to [topic]?"

## Technical Details

### RAG Pipeline Flow
1. **Document Ingestion**
   - Scan Data directory for PDF files
   - Extract text using PdfPig document analysis
   - Split content into 200-token chunks
   - Generate embeddings using Azure OpenAI

2. **Vector Storage**
   - Store chunks in SQLite with vector extensions
   - Index embeddings for fast similarity search
   - Maintain document metadata and versioning

3. **Query Processing**
   - User asks a question in natural language
   - AI automatically calls search function when needed
   - Retrieve top-k most relevant document chunks
   - Generate response based on retrieved context

4. **Response Generation**
   - Combine search results with conversation context
   - Stream response tokens for real-time interaction
   - Include document citations and page references

### Embedding Process Details

The application provides detailed debug logging for the embedding conversion process:

#### During Document Ingestion:
```
[DEBUG] Starting embedding generation for 25 text chunks
[DEBUG] Each chunk will be converted to 1536-dimensional embedding vector
[DEBUG] Using Azure OpenAI text-embedding-3-small model
[DEBUG] Sample chunks being processed:
[DEBUG]   Chunk 1: Page 1 - "Introduction to machine learning concepts..."
[DEBUG]   Text length: 1847 characters
[DEBUG] Embedding generation completed in 2.34 seconds
[DEBUG] Average time per chunk: 93.6ms
[DEBUG] Total vectors created: 25 x 1536 dimensions = 38,400 float values
[DEBUG] Estimated storage size: ~150.0 KB for embeddings
```

#### During Search Operations:
```
[DEBUG] Starting embedding conversion for query: "What is machine learning..."
[DEBUG] Query text length: 45 characters
[DEBUG] Converting query text to 1536-dimensional embedding vector using Azure OpenAI text-embedding-3-small model...
[DEBUG] Embedding conversion and similarity search completed in 156.23ms
[DEBUG] Vector similarity search process:
[DEBUG]   1. Query text converted to 1536-dim embedding vector
[DEBUG]   2. Cosine similarity calculated against stored vectors
[DEBUG]   3. Results ranked by similarity score (0.0 to 1.0)
[DEBUG]   4. Top 5 chunks selected from search
```

### Performance Optimizations
- **Incremental Processing**: Only process new or modified documents
- **Chunking Strategy**: Optimal 200-token chunks for better retrieval
- **Connection Pooling**: Efficient database connections
- **Streaming Responses**: Real-time user experience
- **Batch Embedding**: Efficient vector generation for multiple chunks

## Troubleshooting

### Common Issues

**Application hangs during ingestion:**
- Check PDF file format compatibility
- Verify Azure OpenAI service availability
- Monitor console output for specific error messages
- Review debug logs for embedding conversion bottlenecks

**No search results found:**
- Ensure PDF files contain extractable text
- Verify documents were successfully processed
- Check vector database creation
- Review debug logs for embedding generation issues

**Authentication errors:**
- Verify Azure OpenAI endpoint and API key
- Check network connectivity to Azure services
- Ensure proper authentication permissions

**Performance issues:**
- Monitor debug logs for embedding generation times
- Check vector database size and indexing
- Verify optimal chunk sizes for your content

## Learning Objectives

This demo illustrates key concepts in modern AI applications:

1. **RAG Architecture**: How to combine retrieval and generation
2. **Vector Databases**: Semantic search using embeddings
3. **Function Calling**: AI agents that use tools automatically
4. **Document Processing**: Real-world text extraction and chunking
5. **Azure Integration**: Production AI service consumption
6. **Error Handling**: Robust application design patterns
7. **Embedding Technical Details**: Understanding vector conversion and storage
8. **Performance Optimization**: Efficient vector operations and storage

## Extensions

Potential enhancements for learning and experimentation:

- **Multi-format Support**: Add Word, PowerPoint, or text file processing
- **Advanced Chunking**: Implement hierarchical or semantic chunking
- **Metadata Filtering**: Search by document type, date, or author
- **Web Interface**: Convert to Blazor or ASP.NET Core web app
- **Cloud Storage**: Integrate with Azure Blob Storage or SharePoint
- **Advanced Search**: Add hybrid search (keyword + semantic)
- **Performance Monitoring**: Add embedding performance metrics
- **Vector Optimization**: Implement dimension reduction techniques

## Resources

- [Microsoft.Extensions.AI Documentation](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai)
- [Azure OpenAI Service Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)
- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Vector Database Concepts](https://learn.microsoft.com/en-us/semantic-kernel/memories/)
- [Embedding Models Guide](https://learn.microsoft.com/en-us/azure/ai-services/openai/concepts/models#embeddings)

---

**Built with care by "AI/ML Clarity" for education and demonstration purposes**