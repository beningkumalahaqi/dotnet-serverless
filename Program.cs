using DotnetServerless.Endpoints;
using DotnetServerless.Extensions;

var builder = WebApplication.CreateSlimBuilder(args);

builder.ConfigureServerlessHost();
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseApiPipeline();
app.MapApiEndpoints();

app.Run();
