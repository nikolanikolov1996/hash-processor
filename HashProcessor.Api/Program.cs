using HashProcessor.Api;
using HashProcessor.Api.Services;
using HashProcessor.Database;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var mariaDbConnectionString = builder.Configuration.GetConnectionString("MariaDb") ?? throw new InvalidOperationException("The MariaDB connection string is missing.");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq") ?? throw new InvalidOperationException("The RabbitMQ connection string is missing.");

builder.Services.AddSingleton<IHashRepository>(_ => new HashDatabase(mariaDbConnectionString));
builder.Services.AddSingleton<IHashPublisher>(_ => new HashPublisher(rabbitMqConnectionString));
builder.Services.AddSingleton<HashGenerationService>();
builder.Services.AddSingleton<HashQueryService>();

builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddConcurrencyLimiter("hash-generation", limiterOptions =>
    {
        limiterOptions.PermitLimit = 1;
        limiterOptions.QueueLimit = 0;
    });
});

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.MapControllers();

app.Run();
