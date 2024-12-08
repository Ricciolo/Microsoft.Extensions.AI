using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.ClientModel;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Numerics.Tensors;
using System.Runtime.InteropServices.ComTypes;


await Demo1();
//await Demo2();


async Task Demo1()
{
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
        new OllamaEmbeddingGenerator(new Uri("http://127.0.0.1:11434"), "nomic-embed-text");

    var embedding = await embeddingGenerator.GenerateEmbeddingAsync("Ciao .NET Conference 2024!");
    Console.WriteLine($"Vector length: {embedding.Vector.Length}");

    foreach (var value in embedding.Vector.Span)
    {
        Console.Write($"{value:0.00}, ");
    }
}

async Task Demo2()
{
    Console.WriteLine("Starting...");

    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator =
        new OllamaEmbeddingGenerator(new Uri("http://127.0.0.1:11434"), "nomic-embed-text");

    string[] ristoranti =
    [
        "La Taverna Rustica: Cucina italiana autentica, atmosfera rustica e calda, servizio cordiale e piatti curati nei minimi dettagli.",
        "Sushi Fusion: Fusion asiatica moderna, ambiente minimalista ed elegante, personale attento e vasta selezione di vini pregiati.",
        "Il Faro Blu: Specialità di mare freschissime, vista panoramica sul porto, servizio veloce e piatti ricchi di sapore.",
        "Casa Toscana: Tradizione toscana rivisitata, sala con camino e mattoni a vista, accoglienza familiare e porzioni generose.",
        "Verde Vivo: Cucina vegetariana creativa, giardino interno tranquillo, staff premuroso e ingredienti a chilometro zero.",
        "Grill & Co.: Grigliata di carne e pesce, locale in stile industrial chic, servizio impeccabile e dolci fatti in casa.",
        "Pasta e Pizza: Pasta fatta a mano e pizze gourmet, terrazza con vista, personale gentile e atmosfera rilassante.",
        "Spezie d’Oriente: Specialità indiane piccanti, interni colorati e vivaci, servizio rapido e attenzione alle preferenze personali.",
        "Chez Gourmet: Cucina francese raffinata, illuminazione soffusa, servizio elegante e dessert che sembrano opere d'arte.",
        "Fiesta Mexicana: Cibo messicano autentico, musica dal vivo il fine settimana, ambiente festoso e porzioni abbondanti."
    ];

    var ristorantiEmbeddings = await embeddingGenerator.GenerateAndZipAsync(ristoranti);

    while (true)
    {
        Console.Write("\nChe tipo di ristorante cerchi? ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input)) continue;

        var inputEmbedding = await embeddingGenerator.GenerateEmbeddingAsync(input);

        var closest = from r in ristorantiEmbeddings
            let similarity = TensorPrimitives.CosineSimilarity(r.Embedding.Vector.Span, inputEmbedding.Vector.Span)
            orderby similarity descending
            select new { Ristorante = r.Value, Similarity = similarity };

        foreach (var result in closest.Take(2))
        {
            Console.WriteLine($"{result.Similarity}: {result.Ristorante}");
        }
    }
}
