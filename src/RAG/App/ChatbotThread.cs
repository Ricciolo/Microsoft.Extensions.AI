﻿using System.ComponentModel;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace RAG;

/// <summary>
/// Represents a chat session for answering questions about a specific product.
/// </summary>
/// <param name="chatClient">The chat client used to generate answers.</param>
/// <param name="embeddingGenerator">Generator used for computing message embeddings.</param>
/// <param name="qdrantClient">Client used to search the manual embeddings.</param>
/// <param name="currentProduct">The product that is currently being discussed.</param>
public class ChatbotThread(
    IChatClient chatClient,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    QdrantClient qdrantClient,
    Product currentProduct)
{
    private List<ChatMessage> _messages =
    [
        new ChatMessage(ChatRole.System, $"""
            You are a helpful assistant, here to help customer service staff answer questions they have received from customers.
            The support staff member is currently answering a question about this product:
            ProductId: ${currentProduct.ProductId}
            Brand: ${currentProduct.Brand}
            Model: ${currentProduct.Model}
            """),
        /*
        Answer the user question using ONLY information found by searching product manuals.
            If the product manual doesn't contain the information, you should say so. Do not make up information beyond what is
            given in the product manual.
            
            If this is a question about the product, ALWAYS search the product manual before answering.
            Only search across all product manuals if the user explicitly asks for information about all products.
        */
    ];

    /// <summary>
    /// Answers a user message using information retrieved from product manuals.
    /// </summary>
    /// <param name="userMessage">The question from the user.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The answer text, an optional citation and all context chunks used.</returns>
    public async Task<(string Text, Citation? Citation, string[] AllContext)> AnswerAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        // For a simple version of RAG, we'll embed the user's message directly and
        // add the closest few manual chunks to context.
        var userMessageEmbedding = await embeddingGenerator.GenerateEmbeddingVectorAsync(userMessage, cancellationToken: cancellationToken);
        var closestChunks = await qdrantClient.SearchAsync(
            collectionName: "manuals",
            vector: userMessageEmbedding.ToArray(),
            filter: Qdrant.Client.Grpc.Conditions.Match("productId", currentProduct.ProductId),
            limit: 3, cancellationToken: cancellationToken); // TODO: Evaluate with more or less
        var allContext = closestChunks.Select(c => c.Payload["text"].StringValue).ToArray();

        // Now ask the chatbot
        _messages.Add(new(ChatRole.Assistant, $$"""
            Give an answer using ONLY information from the following product manual extracts.
            If the product manual doesn't contain the information, you should say so. Do not make up information beyond what is given.
            Whenever relevant, specify manualExtractId to cite the manual extract that your answer is based on.

            {{string.Join(Environment.NewLine, closestChunks.Select(c => $"<manual_extract id='{c.Id}'>{c.Payload["text"].StringValue}</manual_extract>"))}}

            User question: {{userMessage}}
            Respond as a JSON object in this format: {
                "ManualExtractId": numberOrNull,
                "ManualQuote": stringOrNull, // The relevant verbatim quote from the manual extract, up to 10 words
                "AnswerText": string
            }
            """));

        var isOllama = chatClient.GetService<OllamaChatClient>() is not null;
        var response = await chatClient.CompleteAsync<ChatBotAnswer>(_messages, cancellationToken: cancellationToken, useNativeJsonSchema: isOllama);
        _messages.Add(response.Message);

        if (response.TryGetResult(out var answer))
        {
            // If the chatbot gave a citation, convert it to info to show in the UI
            var citation = answer.ManualExtractId.HasValue && closestChunks.FirstOrDefault(c => c.Id.Num == (ulong)answer.ManualExtractId) is { } chunk
                ? new Citation((int)chunk.Payload["productId"].IntegerValue, (int)chunk.Payload["pageNumber"].IntegerValue, answer.ManualQuote ?? "")
                : default;

            return (answer.AnswerText, citation, allContext);
        }
        else
        {
            return ("Sorry, there was a problem.", default, allContext);
        }

        /*
        var chatOptions = new ChatOptions
        {
            Tools = [AIFunctionFactory.Create(ManualSearchAsync)]
        };

        _messages.Add(new(ChatRole.User, $$"""
            User question: {{userMessage}}
            Respond in plain text with your answer. Where possible, also add a citation to the product manual
            as an XML tag in the form <cite extractId='number' productId='number'>short verbatim quote</cite>.
            """));
        var response = await chatClient.CompleteAsync(_messages, chatOptions, cancellationToken: cancellationToken);
        _messages.Add(response.Message);
        var answer = ParseResponse(response.Message.Text!);

        // If the chatbot gave a citation, convert it to info to show in the UI
        var citation = answer.ManualExtractId.HasValue
            && (await qdrantClient.RetrieveAsync("manuals", (ulong)answer.ManualExtractId.Value)) is { } chunks
            && chunks.FirstOrDefault() is { } chunk
            ? new Citation((int)chunk.Payload["productId"].IntegerValue, (int)chunk.Payload["pageNumber"].IntegerValue, answer.ManualQuote ?? "")
            : default;

        return (answer.AnswerText, citation);
        */
    }

    [Description("Searches product manuals")]
    private async Task<SearchResult[]> ManualSearchAsync(
        [Description("The product ID, or null to search across all products")] int? productIdOrNull,
        [Description("The search phrase or keywords")] string searchPhrase)
    {
        var searchPhraseEmbedding = (await embeddingGenerator.GenerateAsync([searchPhrase]))[0];
        var closestChunks = await qdrantClient.SearchAsync(
            collectionName: "manuals",
            vector: searchPhraseEmbedding.Vector.ToArray(),
            filter: productIdOrNull is { } productId ? Qdrant.Client.Grpc.Conditions.Match("productId", productId) : (Filter?)default,
            limit: 5);
        return closestChunks.Select(c => new SearchResult((int)c.Id.Num, (int)c.Payload["productId"].IntegerValue, c.Payload["text"].StringValue)).ToArray();
    }

    /// <summary>
    /// Represents a citation from a product manual used as evidence for an answer.
    /// </summary>
    /// <param name="ProductId">Identifier of the product manual.</param>
    /// <param name="PageNumber">Page number within the manual.</param>
    /// <param name="Quote">Short quote from the manual.</param>
    public record Citation(int ProductId, int PageNumber, string Quote);
    private record SearchResult(int ManualExtractId, int ProductId, string ManualExtractText);
    private record ChatBotAnswer(int? ManualExtractId, string? ManualQuote, string AnswerText);

    private static ChatBotAnswer ParseResponse(string text)
    {
        var citationRegex = new Regex(@"<cite extractId='(\d+)' productId='\d*'>(.+?)</cite>");
        if (citationRegex.Match(text) is { Success: true, Groups: var groups } match
            && int.TryParse(groups[1].ValueSpan, out var extractId))
        {
            return new(extractId, groups[2].Value, citationRegex.Replace(text, string.Empty));
        }

        return new(default, default, text);
    }
}
