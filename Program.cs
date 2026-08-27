using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Enable CORS for frontend requests
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});

var geminiApiKey = builder.Configuration["Google:ApiKey"] 
    ?? throw new InvalidOperationException("API Key not found in appsettings.json");

builder.Services.AddSingleton<List<DocumentChunk>>(new List<DocumentChunk>());
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles(); 
app.UseStaticFiles();

// PHASE 2 & 3: PDF Upload (Whole Page Ingestion for better context)
app.MapPost("/api/documents/upload", (
    IFormFile file, 
    List<DocumentChunk> documentStore) =>
{
    if (file == null || file.Length == 0) 
        return Results.BadRequest("No file uploaded.");

    using var stream = file.OpenReadStream();
    using var document = UglyToad.PdfPig.PdfDocument.Open(stream);
    
    documentStore.Clear(); 

    foreach (var page in document.GetPages())
    {
        var text = UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page);
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 20) continue;

        // Store the entire page as one chunk so we don't accidentally cut off coding questions
        documentStore.Add(new DocumentChunk 
        {
            Text = text.Trim(),
            PageNumber = page.Number
        });
    }
    return Results.Ok(new { message = $"Success! Processed {document.NumberOfPages} pages into {documentStore.Count} searchable chunks." });
})
.DisableAntiforgery(); 

// PHASE 4: Direct HTTP Chat Endpoint (Relaxed AI Prompt)
app.MapPost("/api/chat", async (
    [FromBody] ChatRequest request, 
    IHttpClientFactory clientFactory,
    List<DocumentChunk> documentStore) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Results.BadRequest("Question cannot be empty.");

        if (documentStore.Count == 0)
            return Results.BadRequest("Please upload a PDF document first.");

        var questionWords = request.Question.ToLowerInvariant()
            .Split(new[] { ' ', '?', '.', ',', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3) 
            .ToList();

        var topChunks = documentStore
            .Select(chunk => new 
            {
                Chunk = chunk,
                Score = questionWords.Count(w => chunk.Text.ToLowerInvariant().Contains(w))
            })
            .OrderByDescending(x => x.Score)
            .Take(3)
            .ToList();

        var contextText = string.Join("\n\n", topChunks.Select(c => $"[Page {c.Chunk.PageNumber}]: {c.Chunk.Text}"));

        // NEW PROMPT: Allows the AI to solve problems based on the document context
        var prompt = $"You are a helpful academic and coding assistant. Below is text extracted from an uploaded document.\n\n" +
             $"DOCUMENT CONTEXT:\n{contextText}\n\n" +
             $"USER QUESTION: {request.Question}\n\n" +
             $"INSTRUCTIONS:\n" +
             $"1. If the user asks about a policy or rule, answer using ONLY the document.\n" +
             $"2. If the user asks you to solve a problem, provide the full solution and explanation.\n" +
             $"3. Always mention which Page Number the original question/topic was found on.\n" +
             $"4. STRICT FORMATTING RULE: Do NOT use any Markdown formatting. Do not use asterisks (*) for bolding or hashes (#) for headings. Use standard plain text, capital letters for emphasis, and basic numbering for lists.";

        var client = clientFactory.CreateClient();
        var googleApiUrl = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.6-flash:generateContent?key={geminiApiKey}";

        var payload = new 
        {
            contents = new[] 
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await client.PostAsync(googleApiUrl, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            return Results.BadRequest($"Google API Error: {response.StatusCode} - {errorBody}");
        }

        var responseString = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(responseString);
        
        var answer = jsonDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text").GetString();

        return Results.Ok(new 
        { 
            answer = answer,
            sources = topChunks.Select(c => new { page = c.Chunk.PageNumber, preview = c.Chunk.Text.Substring(0, Math.Min(100, c.Chunk.Text.Length)) + "..." })
        });
    }
    catch (Exception ex)
    {
        return Results.BadRequest($"AI Error: {ex.Message}");
    }
});

app.Run();

public class DocumentChunk
{
    public string Text { get; set; } = string.Empty;
    public int PageNumber { get; set; }
}

public class ChatRequest
{
    public string Question { get; set; } = string.Empty;
}