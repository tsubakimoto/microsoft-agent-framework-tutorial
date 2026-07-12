using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddSingleton<DefaultAzureCredential>()
    .AddHttpClient("foundry", client =>
    {
        client.Timeout = Timeout.InfiniteTimeSpan;
    });

builder.Build().Run();
