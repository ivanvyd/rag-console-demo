// ================================================================
// AI/ML Clarity Demo - Terminal RAG Application
// ================================================================
// This console application demonstrates a complete Retrieval-Augmented Generation (RAG) system
// that processes PDF documents, creates semantic embeddings, and provides AI-powered chat 
// with automatic document search capabilities.
//
// Key Features:
// - PDF document ingestion and text extraction
// - Vector-based semantic search using embeddings
// - Interactive AI chat with function calling
// - Local SQLite vector database storage
// - Real-time streaming responses
// - Comprehensive token usage tracking and cost monitoring
// ================================================================

using AiMlClarityDemoTerminal.Entities;
using AiMlClarityDemoTerminal.Ingestion;
using AiMlClarityDemoTerminal.Services;
using AiMlClarityDemoTerminal.Sources;
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

// Initialize dependency injection container for managing services
IServiceCollection services = new ServiceCollection();

// ================================================================
// Token Usage Tracking Variables
// ================================================================
// Track cumulative token usage across the entire session for cost monitoring
long totalInputTokens = 0;
long totalOutputTokens = 0;
long totalEmbeddingTokens = 0;

// ================================================================
// Step 1: Azure OpenAI Configuration
// ================================================================
// Configure connection to Azure OpenAI service for chat completion and embeddings.
// In a production environment, these values should be stored securely in configuration
// files, environment variables, or Azure Key Vault.

// PUT YOUR AZURE OPENAI ENDPOINT, API KEY AND MODELS HERE
string endpoint = "your-azure-open-ai-endpoint";
string apiKey = "your-api-key";
string llmModel = "your-llm-model"; // Chat completion model for generating responses
string embeddingModel = "text-embedding-3-small"; // Embedding model for creating vector representations

// Create Azure OpenAI client with authentication credentials
AzureOpenAIClient azureOpenAi = new(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey));

// Initialize chat client for conversational AI interactions
// This client handles streaming responses and function calling
IChatClient chatClient = azureOpenAi
    .GetChatClient(llmModel)
    .AsIChatClient();

// Initialize embedding generator for converting text to vector representations
// Used to create semantic embeddings for document chunks during ingestion
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = azureOpenAi
    .GetEmbeddingClient(embeddingModel)
    .AsIEmbeddingGenerator();

// Register embedding generator in dependency injection container
services.AddEmbeddingGenerator(embeddingGenerator);

// ================================================================
// Step 2: Vector Database Configuration
// ================================================================
// Set up local SQLite vector database for storing document embeddings and metadata.
// This provides fast semantic search capabilities for the RAG system.

// Find the project directory dynamically to ensure database and data files
// are stored in the correct location regardless of execution context
string projectDirectory = FindProjectDirectory();
Console.WriteLine($"[FOLDER] Project directory: {projectDirectory}");

// Configure SQLite database path for vector storage
string vectorStoreConnectionString = $"Data Source={Path.Combine(projectDirectory, "aimlclaritydemo.db")}";
Console.WriteLine($"[DATABASE] Vector store: {vectorStoreConnectionString}");

// Register vector collections for document chunks and metadata
// IngestedChunk: Stores text chunks with their vector embeddings for semantic search
// IngestedDocument: Stores document metadata and versioning information
services.AddSqliteCollection<string, IngestedChunk>("data-chunks",
    vectorStoreConnectionString);

services.AddSqliteCollection<string, IngestedDocument>("data-documents",
    vectorStoreConnectionString);

// ================================================================
// Step 3: Service Registration
// ================================================================
// Register core services required for the RAG pipeline operation.

services.AddScoped<DataIngestor>();           // Orchestrates document ingestion process
services.AddSingleton<SemanticSearch>();     // Provides vector-based document search
services.AddChatClient(chatClient).UseFunctionInvocation();  // Enable AI function calling
services.AddEmbeddingGenerator(embeddingGenerator);          // Re-register for DI

// ================================================================
// Step 4: Document Ingestion Pipeline
// ================================================================
// Process PDF documents from the Data directory and store them in the vector database.
// This step only processes new or modified documents to optimize performance.

using ServiceProvider serviceProvider = services.BuildServiceProvider();

Console.WriteLine("[INIT] Initializing RAG system and ingesting PDF documents...");

// Configure source directory for PDF documents
string dataDirectory = Path.Combine(projectDirectory, "Data");
Console.WriteLine($"[SCAN] Looking for PDFs in: {dataDirectory}");

// Create PDF directory source for document ingestion
LocalPdfDirectorySource localPdfDirectorySource = new(dataDirectory);

// Get document ingestion service from DI container
var ingestor = serviceProvider.GetRequiredService<DataIngestor>();

try
{
    // Execute document ingestion with timeout protection to prevent hanging
    // This processes PDFs, extracts text, creates embeddings, and stores in vector database
    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
    
    var ingestionTask = ingestor.IngestDataAsync(localPdfDirectorySource);
    var timeoutTask = Task.Delay(Timeout.Infinite, cts.Token);
    
    var completedTask = await Task.WhenAny(ingestionTask, timeoutTask);
    
    if (completedTask == timeoutTask)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[TIMEOUT] Ingestion timed out after 5 minutes. This might indicate a hang in the process.");
        Console.ResetColor();
        return;
    }
    
    await ingestionTask; // Propagate any exceptions from ingestion
    Console.WriteLine("[SUCCESS] Document ingestion completed!\n");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR] Ingestion failed: {ex.Message}");
    Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
    Console.ResetColor();
    return;
}

// ================================================================
// Step 5: RAG Chat System Setup
// ================================================================
// Configure the AI chat system with automatic document search capabilities.
// The AI can automatically search through documents when answering questions.

var semanticSearch = serviceProvider.GetRequiredService<SemanticSearch>();
var ragChatClient = serviceProvider.GetRequiredService<IChatClient>();

// Define the search function that the AI can automatically invoke
// This enables the AI to search through documents when it needs information
AIFunction searchDocumentsFunction = AIFunctionFactory.Create(
    async (string query, int maxResults = 5) =>
    {
        // Log search activity for debugging and demonstration purposes
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"[SEARCH] Searching for: {query}");
        Console.ResetColor();
        
        // Track search embedding tokens
        var searchStartTime = DateTime.UtcNow;
        
        // Perform semantic search through the vector database
        var results = await semanticSearch.SearchAsync(query, null, maxResults);
        
        var searchEndTime = DateTime.UtcNow;
        var searchDuration = searchEndTime - searchStartTime;
        
        // Estimate tokens for search query (rough approximation: 1 token ≈ 4 characters)
        long estimatedSearchTokens = query.Length / 4;
        totalEmbeddingTokens += estimatedSearchTokens;
        
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine($"[TOKENS] Search embedding - Estimated tokens: {estimatedSearchTokens:N0} " +
                         $"(Duration: {searchDuration.TotalMilliseconds:F0}ms)");
        Console.ResetColor();
        
        // Handle case where no relevant documents are found
        if (!results.Any())
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[WARNING] No relevant documents found for this query.");
            Console.ResetColor();
            return "No relevant documents found for this query.";
        }
        
        // Log successful search results
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[FOUND] Found {results.Count} relevant document chunks");
        Console.ResetColor();
        
        // Format search results for AI consumption with document citations
        var searchResults = results.Select((chunk, index) => 
            $"[Result {index + 1}] Document: {chunk.DocumentId} (Page {chunk.PageNumber})\nContent: {chunk.Text}\n---");
        
        return string.Join("\n", searchResults);
    },
    "SearchDocuments",  // Function name that the AI will use
    "Search through the ingested PDF documents for relevant information based on the user's query");

// ================================================================
// Step 6: Interactive Console Chat Loop
// ================================================================
// Provide an interactive chat interface where users can ask questions about their documents.
// The AI automatically searches through documents and provides informed responses.

Console.WriteLine("=== AI/ML Clarity Demo - RAG Console Chat ===");
Console.WriteLine("[INFO] I can search through your PDF documents to answer questions.");
Console.WriteLine("[HELP] Ask me anything about the content in your PDFs!");
Console.WriteLine("[EXAMPLES] Try questions like:");
Console.WriteLine("   * 'What is this document about?'");
Console.WriteLine("   * 'Summarize the main points'");
Console.WriteLine("   * 'Tell me about [specific topic]'");
Console.WriteLine("[EXIT] Type 'exit' to quit\n");

// Initialize conversation with system prompt that defines AI behavior
List<ChatMessage> conversation = [
    new ChatMessage(ChatRole.System, 
        "You are a helpful AI assistant that can search through PDF documents to answer user questions. " +
        "When users ask questions about the documents, use the SearchDocuments function to find relevant information. " +
        "Always base your answers on the search results from the function calls. " +
        "Cite the specific documents and page numbers when providing information. " +
        "If no relevant information is found, let the user know politely.")
];

// Configure chat options with the search function tool
ChatOptions chatOptions = new()
{
    Tools = [searchDocumentsFunction]  // Enable automatic function calling
};

// Main interactive chat loop
while (true)
{
    // Get user input with colored prompt
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.Write("[USER] You: ");
    Console.ResetColor();
    string? userInput = Console.ReadLine();
    
    // Check for exit conditions
    if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
    {
        break;
    }
    
    // Add user message to conversation history
    conversation.Add(new ChatMessage(ChatRole.User, userInput));
    
    try
    {
        // Generate AI response with streaming for real-time experience
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.Write("[AI] Assistant: ");
        Console.ResetColor();
        
        // Track chat completion timing and tokens
        var chatStartTime = DateTime.UtcNow;
        
        // Stream the response tokens as they are generated
        var response = ragChatClient.GetStreamingResponseAsync(conversation, chatOptions);
        
        string assistantResponse = "";
        List<ChatResponseUpdate> updates = [];
        
        await foreach (var update in response)
        {
            if (update.Text != null)
            {
                Console.Write(update.Text); // Display tokens in real-time
                assistantResponse += update.Text;
            }
            updates.Add(update);
        }
        
        var chatEndTime = DateTime.UtcNow;
        var chatDuration = chatEndTime - chatStartTime;
        
        // Convert streaming updates to a ChatResponse to access usage information
        if (updates.Count > 0)
        {
            var chatResponse = updates.ToChatResponse();
            if (chatResponse.Usage != null)
            {
                var usage = chatResponse.Usage;
                var inputTokens = usage.InputTokenCount ?? 0;
                var outputTokens = usage.OutputTokenCount ?? 0;
                var totalTokens = inputTokens + outputTokens;
                
                // Update cumulative counters
                totalInputTokens += inputTokens;
                totalOutputTokens += outputTokens;
                
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[TOKENS] Chat completion - Input: {inputTokens:N0}, Output: {outputTokens:N0}, " +
                                 $"Total: {totalTokens:N0} (Duration: {chatDuration.TotalSeconds:F1}s)");

                Console.ResetColor();
            }
            else
            {
                // Fallback when usage information isn't available
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[TOKENS] Chat completion duration: {chatDuration.TotalSeconds:F1}s " +
                                 "(Token usage information not available in this response)");
                Console.ResetColor();
            }
        }
        else
        {
            // No updates received
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"[TOKENS] Chat completion duration: {chatDuration.TotalSeconds:F1}s " +
                             "(No response updates received)");
            Console.ResetColor();
        }
        
        Console.WriteLine();
        
        // Add complete response to conversation history for context
        conversation.Add(new ChatMessage(ChatRole.Assistant, assistantResponse));
    }
    catch (Exception ex)
    {
        // Handle and display any errors during chat interaction
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] Error: {ex.Message}\n");
        Console.ResetColor();
    }
}

// Display session summary when user exits
Console.ForegroundColor = ConsoleColor.Yellow;
Console.WriteLine("[GOODBYE] Goodbye! Thanks for using the RAG demo!");

// Display comprehensive session token usage summary
Console.ForegroundColor = ConsoleColor.White;
Console.WriteLine("\n=== SESSION TOKEN USAGE SUMMARY ===");
Console.WriteLine($"Chat Input Tokens:    {totalInputTokens:N0}");
Console.WriteLine($"Chat Output Tokens:   {totalOutputTokens:N0}");
Console.WriteLine($"Embedding Tokens:     {totalEmbeddingTokens:N0} (estimated)");
Console.WriteLine($"Total Chat Tokens:    {totalInputTokens + totalOutputTokens:N0}");
Console.WriteLine($"Grand Total Tokens:   {totalInputTokens + totalOutputTokens + totalEmbeddingTokens:N0}");

// Calculate session cost estimates
decimal sessionInputCost = (decimal)totalInputTokens * 0.000001m;
decimal sessionOutputCost = (decimal)totalOutputTokens * 0.000002m; 
decimal sessionEmbeddingCost = (decimal)totalEmbeddingTokens * 0.0000001m; // ~$0.1 per 1M tokens
decimal sessionTotalCost = sessionInputCost + sessionOutputCost + sessionEmbeddingCost;

Console.WriteLine($"\nEstimated Session Cost: ${sessionTotalCost:F6}");
Console.WriteLine("(Note: Actual costs may vary by model, region, and current pricing)");
Console.ResetColor();

/// <summary>
/// Finds the project directory by walking up the directory tree to locate the .csproj file.
/// This ensures that data files and databases are stored in the correct location
/// regardless of how the application is executed (Visual Studio, dotnet run, etc.).
/// </summary>
/// <returns>The full path to the project directory containing the .csproj file.</returns>
static string FindProjectDirectory()
{
    var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());

    // Walk up the directory tree to find the .csproj file
    while (currentDirectory != null)
    {
        if (currentDirectory.GetFiles("*.csproj").Any())
        {
            return currentDirectory.FullName;
        }
        currentDirectory = currentDirectory.Parent;
    }

    // Fallback: use the directory where the assembly is located and walk up
    var assemblyLocation = Assembly.GetExecutingAssembly().Location;
    currentDirectory = new DirectoryInfo(Path.GetDirectoryName(assemblyLocation)!);

    while (currentDirectory != null)
    {
        if (currentDirectory.GetFiles("*.csproj").Any())
        {
            return currentDirectory.FullName;
        }
        currentDirectory = currentDirectory.Parent;
    }

    // Last resort fallback to current directory
    return Directory.GetCurrentDirectory();
}