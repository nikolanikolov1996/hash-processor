using HashProcessor.Database;
using HashProcessor.Worker;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

var mariaDbConnectionString = builder.Configuration.GetConnectionString("MariaDb") ?? throw new InvalidOperationException("The MariaDB connection string is missing.");
var rabbitMqConnectionString = builder.Configuration.GetConnectionString("RabbitMq") ?? throw new InvalidOperationException("The RabbitMQ connection string is missing.");

builder.Services.AddSingleton<IHashRepository>(_ => new HashDatabase(mariaDbConnectionString));
builder.Services.AddSingleton(
    new ConnectionFactory
    {
        Uri = new Uri(rabbitMqConnectionString),
        AutomaticRecoveryEnabled = true,
        ConsumerDispatchConcurrency = 4
    });

builder.Services.AddSingleton<HashMessageProcessor>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
