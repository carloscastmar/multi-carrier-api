using System.Diagnostics;
using MultiCarrierApi.Infrastructure.Data;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.AddServiceDefaults();

builder.AddKeyVaultIfConfigured();
builder.AddApplicationServices();
builder.AddInfrastructureServices();
builder.AddWebServices();

var app = builder.Build();

app.UseExceptionHandler();

// Echo the trace id so callers can quote it when reporting a problem.
app.Use(async (context, next) =>
{
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Trace-Id"] = traceId;
        return Task.CompletedTask;
    });

    await next();
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    await app.InitialiseDatabaseAsync();
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors(static builder => 
    builder.AllowAnyMethod()
        .AllowAnyHeader()
        .AllowAnyOrigin());

app.UseFileServer();

app.MapOpenApi();
app.MapScalarApiReference();

app.Map("/", () => Results.Redirect("/scalar"));

app.MapDefaultEndpoints();
app.MapEndpoints(typeof(Program).Assembly);


app.Run();
