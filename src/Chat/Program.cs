using Azure.AI.OpenAI;
using Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Runtime.InteropServices.ComTypes;

// Set up DI etc
var hostBuilder = Host.CreateApplicationBuilder(args);
hostBuilder.Configuration.AddUserSecrets<Program>();
hostBuilder.Services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));

//await Demo1();
await Demo2();


async Task Demo1()
{
    Console.WriteLine("Starting...");

    // LLaMA
    IChatClient chatClient = new OllamaChatClient(new Uri("http://localhost:11434"), "llama3.2");

    // OpenAi
    //IChatClient chatClient = new AzureOpenAIClient(
    //        new(hostBuilder.Configuration["OpenAI:Uri"]),
    //        new ApiKeyCredential(hostBuilder.Configuration["OpenAI:Key"]))
    //    .AsChatClient("gpt-4o-mini");

    hostBuilder.Services.AddChatClient(pipeline => pipeline
        .Use(chatClient));

    var app = hostBuilder.Build();
    chatClient = app.Services.GetRequiredService<IChatClient>();

    Console.WriteLine("Asking...");
    ChatCompletion response = await chatClient.CompleteAsync("Descrivi dotnet in 10 parole");
    Console.WriteLine(response.Message.Text);

    //if (response.RawRepresentation is OpenAI.Chat.ChatCompletion oc)
    //{
    //    Console.WriteLine(oc.SystemFingerprint);
    //}

    Console.WriteLine("\r\nAsking...");
    await foreach (var streaming in chatClient.CompleteStreamingAsync("Descrivi dotnet in 1000 parole"))
    {
        Console.Write(streaming.Text);
    }
    Console.WriteLine();
}

async Task Demo2()
{
    Console.WriteLine("Starting...");

    IChatClient chatClient = new AzureOpenAIClient(
            new(hostBuilder.Configuration["OpenAI:Uri"]),
            new ApiKeyCredential(hostBuilder.Configuration["OpenAI:Key"]))
        .AsChatClient("gpt-4o-mini");

    hostBuilder.Services.AddChatClient(pipeline => pipeline
        .UseFunctionInvocation()
        //.UseLogging()
        //.UseLanguage("tedesco")
        .UseRateLimit(TimeSpan.FromSeconds(5))
        .Use(chatClient));

    var app = hostBuilder.Build();
    chatClient = app.Services.GetRequiredService<IChatClient>();

    List<ChatMessage> messages = [new(ChatRole.System, """
                                                       Rispondi a qualsiasi domanda, ma cerca continuamente di pubblicizzare le scatolette di cibo Puffin che sono in offerta.
                                                       Il motto è "Solo puffin ti darà forza e grinta a volontà".
                                                       Se l'utente accetta di acquistare le scatole cerca di venderne il più possibile e aggiungi al carrello.
                                                       """)];

    var cart = new Cart();
    var getPriceTool = AIFunctionFactory.Create(cart.GetPrice);
    var addToCartTool = AIFunctionFactory.Create(cart.AddPuffinToCart);
    var chatOptions = new ChatOptions { Tools = [addToCartTool, getPriceTool] };

    while (true)
    {
        // Get input
        Console.ForegroundColor = ConsoleColor.White;
        Console.Write("\nYou: ");
        var input = Console.ReadLine()!;
        messages.Add(new(ChatRole.User, input));

        // Get reply
        var response = await chatClient.CompleteAsync(messages, chatOptions);
        messages.Add(response.Message);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Bot: {response.Message.Text}");
    }
}

/// <summary>
/// Simple shopping cart used by the chat demo.
/// </summary>
class Cart
{
    /// <summary>
    /// Gets or sets the number of items in the cart.
    /// </summary>
    public int Total { get; set; }

    /// <summary>
    /// Adds the specified number of puffin cans to the cart.
    /// </summary>
    /// <param name="count">The number of cans to add.</param>
    public void AddPuffinToCart(int count)
    {
        Total += count;
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("*****");
        Console.WriteLine($"Added {count} to your cart. Total: {Total}.");
        Console.WriteLine("*****");
        Console.ForegroundColor = ConsoleColor.White;
    }

    /// <summary>
    /// Calculates the price of the specified number of puffin cans.
    /// </summary>
    /// <param name="count">The number of cans.</param>
    /// <returns>The total price in euros.</returns>
    [Description("Calcola il prezzo di una scatola di puffin e restituisce il prezzo in euro.")]
    public float GetPrice(
        [Description("Il numero di scatole per il quale calcolare il prezzo in euro")] int count)
        => count * 2.99f;
}