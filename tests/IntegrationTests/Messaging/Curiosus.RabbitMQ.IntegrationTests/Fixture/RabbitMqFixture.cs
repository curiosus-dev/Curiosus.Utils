using System;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;
using Xunit;

namespace Curiosus.RabbitMQ.IntegrationTests.Fixture;

/// <summary>
/// RabbitMQ broker in a Docker container, shared by all tests of <see cref="RabbitMqCollection"/>.
/// </summary>
public class RabbitMqFixture : IAsyncLifetime
{
    public const string UserName = "guest";
    public const string Password = "guest";

    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4.1-alpine")
        .WithUsername(UserName)
        .WithPassword(Password)
        .Build();

    public string HostName => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = HostName,
            Port = Port,
            UserName = UserName,
            Password = Password
        };

        return factory.CreateConnectionAsync(cancellationToken);
    }

    public async Task<uint> GetReadyMessagesCountAsync(string queueName, CancellationToken cancellationToken)
    {
        await using var connection = await CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        return await channel.MessageCountAsync(queueName, cancellationToken);
    }

    /// <summary>
    /// Closes all client connections on the broker side, as a broker restart or a network failure does.
    /// </summary>
    public async Task CloseAllConnectionsAsync(CancellationToken cancellationToken)
    {
        var result = await _container.ExecAsync(["rabbitmqctl", "close_all_connections", "test"], cancellationToken);
        if (result.ExitCode != 0) throw new InvalidOperationException($"Failed to close connections: {result.Stderr}");
    }

    public static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}

[CollectionDefinition(Name)]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "RabbitMQ";
}
