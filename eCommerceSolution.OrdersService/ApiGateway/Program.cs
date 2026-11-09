using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Polly;  //🔥 integra le funzionalita di resilience della lib Polly, anche nel gateway.

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);  //carichi ur file ocelot.json. optional:false se non c'è il file allora fallisce, reloadOnChange:true il provider di configurazione rileva le modifiche al file e aggiorna i valori caricati
//🔥🔥 ocelot.json serve per riconoscere la rotta e instradare correttamente (eseguito da ocelot, setti tutto nel file)

builder.Services
  .AddOcelot()
  .AddPolly();

var app = builder.Build();
await app.UseOcelot();  //rende operativo il Gateway 

app.Run();

//APIGateway, a differenza del OrdersMicroservice.API che gestisce le richieste relative agli ordini, questo è il punto di ingresso che utilizzera il frontend.

