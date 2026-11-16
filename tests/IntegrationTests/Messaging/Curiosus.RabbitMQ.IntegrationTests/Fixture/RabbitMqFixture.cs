using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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

    private readonly RabbitMqContainer _container = CreateContainer();

    public static RabbitMqContainer CreateContainer() => new RabbitMqBuilder("rabbitmq:4.1-alpine")
        .WithUsername(UserName)
        .WithPassword(Password)
        .Build();

    public string HostName => _container.Hostname;

    public int Port => _container.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort);

    public Uri ConnectionUri => GetConnectionUri(_container);

    public static Uri GetConnectionUri(RabbitMqContainer container)
        => new($"amqp://{UserName}:{Password}@{container.Hostname}:"
               + $"{container.GetMappedPublicPort(RabbitMqBuilder.RabbitMqPort)}/");

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
        await RabbitMqCtlAsync(cancellationToken, "close_all_connections", "test");
    }

    /// <summary>
    /// Returns the number of channels of each open connection named <paramref name="connectionName"/>.
    /// </summary>
    public async Task<IReadOnlyList<int>> GetChannelCountsAsync(string connectionName, CancellationToken cancellationToken)
    {
        var output = await RabbitMqCtlAsync(
            cancellationToken, "list_connections", "client_properties", "channels", "--formatter", "json", "--quiet");

        using var json = JsonDocument.Parse(output);
        return json.RootElement.EnumerateArray()
            .Where(connection => FindConnectionName(connection.GetProperty("client_properties")) == connectionName)
            .Select(connection => connection.GetProperty("channels").GetInt32())
            .ToArray();
    }

    // client properties are rendered as [name, type, value] triples or as an object, depending on the version
    private static string? FindConnectionName(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("connection_name", out var value) && value.ValueKind == JsonValueKind.String)
                    return value.GetString();
                return element.EnumerateObject().Select(p => FindConnectionName(p.Value)).FirstOrDefault(n => n != null);
            case JsonValueKind.Array:
                var items = element.EnumerateArray().ToArray();
                if (items.Length == 3 && items[0].ValueKind == JsonValueKind.String
                    && items[0].GetString() == "connection_name" && items[2].ValueKind == JsonValueKind.String)
                    return items[2].GetString();
                return items.Select(FindConnectionName).FirstOrDefault(n => n != null);
            default:
                return null;
        }
    }

    private async Task<string> RabbitMqCtlAsync(CancellationToken cancellationToken, params string[] arguments)
    {
        var result = await _container.ExecAsync(["rabbitmqctl", .. arguments], cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"rabbitmqctl {String.Join(' ', arguments)} failed: {result.Stderr}");
        }

        return result.Stdout;
    }

    public static string UniqueName(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}

[CollectionDefinition(Name)]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "RabbitMQ";
}
