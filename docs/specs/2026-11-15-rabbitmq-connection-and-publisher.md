# RabbitMQ: shared connection and confirmed publisher

- Status: Implemented
- Date: 2026-11-15
- Issue: [#109](https://github.com/curiosus-dev/Curiosus.Utils/issues/109) (part 1 of 3), options from
  [#75](https://github.com/curiosus-dev/Curiosus.Utils/issues/75), options convention of
  [#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44)
- ADRs: none; the thin-client decision is [#74](https://github.com/curiosus-dev/Curiosus.Utils/issues/74)

## Problem and goal

The consumer app holds about 1.06k lines of hand-written publishers on the RabbitMQ.Client 6 API: each opens its own
connection, runs its own reconnect loop on top of auto-recovery, buffers messages in an internal queue and reports a
publish as sent without a publisher confirm. RabbitMQ.Client 7, which `Curiosus.RabbitMQ` requires since 3.0.0, has
no sync API, so this code has to be rewritten anyway.

Done when an app registers RabbitMQ once, gets one connection for everything new in the package, and publishes with
`IRabbitPublisher`: `PublishAsync` completes only when the broker confirmed the message, and fails with a typed
exception, within a configured timeout, when it did not.

## Scope

In scope (`Curiosus.RabbitMQ` 4.0.0, first alpha):

- `RabbitMQOptions` in the [#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44) convention and
  `AddCuriosusRabbitMQ`;
- `IRabbitConnectionProvider`: one lazily opened connection per app with automatic recovery;
- `IRabbitMQMessageSerializer`, symmetric, System.Text.Json by default, and `RabbitMQJson.DefaultOptions`;
- `IRabbitPublisher` with publisher confirms, `PublishTimeout` and a small channel pool;
- RabbitMQ.Client 7.0.0 → 7.2.1 (see Requirements);
- README section, CHANGELOG entries, the "Hand-written publishers" part of `docs/migrations/curiosus-rabbitmq/4.0.md`.

Out of scope (non-goals):

- consumer host, `RabbitMQMessage<T>`, `IRabbitConsumerControl`, RPC server, `IRabbitQueueStats`: parts 2 and 3 of
  [#109](https://github.com/curiosus-dev/Curiosus.Utils/issues/109), with the open points on dead-lettering,
  channel recovery and quorum queues;
- the RPC client of [#75](https://github.com/curiosus-dev/Curiosus.Utils/issues/75): reply modes, timeout,
  `IRabbitRpcClient` and moving it onto the shared connection. Its own connection and reconnect logic stay until then;
- declaring exchanges and queues: the publisher publishes to what exists;
- buffering or an outbox: a publish that can't be confirmed fails, nothing is kept in memory;
- metrics ([#78](https://github.com/curiosus-dev/Curiosus.Utils/issues/78)) and the sample app (end of #109).

## Requirements

Functional:

1. `services.AddCuriosusRabbitMQ()` binds `RabbitMQOptions` from `RabbitMQOptions.SectionName` (`"RabbitMQ"`) or another
   section with `AddCuriosusOptions`, applies an optional `configure` action after binding, validates them when
   the host starts and registers the connection provider, the default serializer, the publisher and
   `RabbitMqRpcClientFactory` (which `AddRabbitMQRPC` registered) as singletons, plus `TimeProvider.System`.
   Registrations use `TryAdd`, so an app can register its own `IRabbitMQMessageSerializer` before or after the call.
   Only the first call binds its section and registers the validation; a later call adds its `configure` only.
2. The connection provider opens one `IConnection` on first use, named `ClientName`, with `AutomaticRecoveryEnabled`
   and topology recovery on. Concurrent first calls share one open attempt, which doesn't use any caller's token: a
   caller that cancels stops waiting, the others keep waiting. A failed open (`BrokerUnreachableException`) is not
   cached: the next call tries again. For `amqps` the provider resets `Ssl.AcceptablePolicyErrors` to `None`, which
   RabbitMQ.Client relaxes to accept a host name mismatch when it parses the URI. The provider implements
   `IAsyncDisposable` and `IDisposable` and closes the connection when the service provider is disposed.
3. The RPC client keeps its own connection, built from `ConnectionUri` and named `{ClientName}_rpc_client` as now. The
   exchange it publishes requests to, `RabbitMQOptions.ExchangeName` today, becomes an optional `requestExchange`
   argument of `RabbitMqRpcClientFactory.CreateClientAsync` (default: the default exchange `""`, as the option was).
   The factory constructor takes `IOptions<RabbitMQOptions>`.
4. `PublishAsync` serializes the message with `IRabbitMQMessageSerializer`, sets `content-type` from it, publishes on
   a channel opened with publisher confirmations and confirmation tracking, and completes when the broker acks. Each
   publish gets its own `BasicProperties` and headers dictionary: RabbitMQ.Client adds its `x-dotnet-pub-seq-no`
   header to them (a constant of the client, which matches returns to publishes by it), and consumers see it.
5. Properties: `MessageId` (a new GUID v7 when not set), `CorrelationId`, `Persistent` (default `true`), `Mandatory`
   (default `false`), `Headers`. `timestamp` is set from `TimeProvider`.
6. A publish fails with `RabbitPublishException` and a `Reason`:
   - `Nacked`: the broker nacked it;
   - `Returned`: a `Mandatory` message could not be routed, with the reply code and text of the return;
   - `ChannelClosed`: the broker closed the channel while the publish was in flight, with the close reply code and text
     (404 for a missing exchange). RabbitMQ.Client fails every publish in flight on that channel with the same close
     reason, so the publish that caused it can't be told from the others: their outcome is unknown;
   - `ConnectionLost`: the connection closed while the publish was in flight: outcome unknown;
   - `Timeout`: no connection, no pool channel or no confirm within `PublishTimeout`.
7. `PublishTimeout` (default 30 s) bounds the whole call. While the connection is down or recovering, RabbitMQ.Client
   fails a publish at once, so the publisher checks before sending: when the pool channel or the connection isn't
   open, it waits for recovery (`RecoverySucceededAsync` or the connection open again) and sends then, or fails with
   `Timeout`. A connection that can't be opened is tried again every second; the `Timeout` exception carries the last
   error as `InnerException`. Opening a pool channel is bounded by the same timeout: RabbitMQ.Client waits for
   `channel.open-ok` up to its continuation timeout (20 s) whatever the token, so a channel opened too late is aborted.
   Once `BasicPublishAsync` was called, the publish is never re-sent.
8. Cancellation by the caller's token throws `OperationCanceledException`, not `RabbitPublishException`.
9. Pool channels: a channel closed by the broker while its connection stays open is aborted at once, in the
   background (`AbortAsync`, so a connection recovery before the next publish on it doesn't bring it back), and replaced
   and disposed (not awaited under the slot's lock) on the next publish that needs it. A publish that took the channel
   before it was replaced and gets `ObjectDisposedException` fails like one in flight (`ChannelClosed`). A channel closed with its connection is kept: auto-recovery re-creates
   it with confirmations on; one still closed after `RecoverySucceededAsync` is replaced too.
10. RabbitMQ.Client goes from 7.0.0 to 7.2.1, the minimum version for consumers: 7.0.0 releases a lock it never took
    when a publish is cancelled while waiting for the channel (two senders on one channel, or a `SemaphoreFullException`
    that hides the cancellation), and has no reply code and text on a return (`PublishReturnException`, 7.2.0).

Non-functional:

1. Thread safety: the publisher, the provider and the serializer are safe for concurrent use.
2. Throughput: concurrent publishes go through `PublisherChannelCount` channels (default 4). A channel takes
   concurrent publishes; their number is bounded by the caller, not by a limiter (the channels are created without
   `OutstandingPublisherConfirmationsRateLimiter`, so its throttling and "queue full" failures don't apply).
3. Security: the connection URI is never logged or put into an exception with its user info. Connection events log
   host, port, virtual host and `ClientName`. `LogConfigurationOnStart` masks the URI already
   (`amqp://user:pass@` is a secret value).
4. Logging: the provider logs connection open (Information), shutdown, blocked and recovery (Warning / Information).
   The publisher doesn't log failures it throws.
5. Compatibility: `RabbitMQJson.DefaultOptions` keeps the wire format of `RabbitMqRpcClient.DefaultJsonSerializerOptions`.

## Acceptance scenarios

```gherkin
Scenario: confirmed publish
  Given an app registered with AddCuriosusRabbitMQ and a queue bound to exchange "orders"
  When it publishes an OrderPaid to "orders" with routing key "paid"
  Then PublishAsync completes after the broker confirmed the message
  And the queue holds one persistent message with content-type "application/json", a message id and the JSON body

Scenario: missing exchange
  When the app publishes to an exchange that doesn't exist
  Then PublishAsync throws RabbitPublishException with Reason ChannelClosed and reply code 404
  And the next publish to an existing exchange succeeds
  And after a connection recovery the broker lists no extra channel

Scenario: unroutable mandatory message
  When the app publishes a Mandatory message that no queue is bound for
  Then PublishAsync throws RabbitPublishException with Reason Returned and reply code 312

Scenario: broker unreachable
  Given PublishTimeout is 2 seconds and the broker doesn't answer
  When the app publishes
  Then PublishAsync throws RabbitPublishException with Reason Timeout after about 2 seconds

Scenario: connection recovery
  Given the broker closed all connections
  When the app publishes during and after auto-recovery
  Then the publishes are confirmed on the recovered connection

Scenario: one connection
  When the publisher is used from several threads
  Then the broker lists one connection named after ClientName with PublisherChannelCount channels at most
```

## Contracts

```csharp
public class RabbitMQOptions
{
    public const string SectionName = "RabbitMQ";             // default configuration section

    [Required]
    public Uri ConnectionUri { get; set; } = null!;          // amqp:// or amqps://user:pass@host:port/vhost

    [Required, RegularExpression("^[A-Za-z0-9._-]{1,64}$")]
    public string ClientName { get; set; } = Environment.MachineName;

    public TimeSpan PublishTimeout { get; set; } = TimeSpan.FromSeconds(30);

    [Range(1, 64)]
    public int PublisherChannelCount { get; set; } = 4;
}
// RabbitMQOptionsValidator (IValidateOptions): an absolute URI with the amqp/amqps scheme and a host, user info that
// RabbitMQ.Client can parse (it throws later with the password in the message otherwise), one path segment at most
// (the virtual host; RabbitMQ.Client throws on more), 0 < PublishTimeout <= MaxPublishTimeout (about 49.7 days, the
// CancellationTokenSource limit). Error messages never contain the URI.

// binds the section, then applies configure; a missing section binds nothing (defaults + configure)
public static OptionsBuilder<RabbitMQOptions> AddCuriosusRabbitMQ(this IServiceCollection services,
    string configSectionPath = RabbitMQOptions.SectionName, Action<RabbitMQOptions>? configure = null);

public interface IRabbitConnectionProvider
{
    ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}

public interface IRabbitMQMessageSerializer
{
    string ContentType { get; }                                  // "application/json"
    ReadOnlyMemory<byte> Serialize<T>(T message);
    T Deserialize<T>(ReadOnlyMemory<byte> body, string? contentType);   // other content types: NotSupportedException
}

public static class RabbitMQJson
{
    public static JsonSerializerOptions DefaultOptions { get; }  // read-only, the RPC client's current options
}

public interface IRabbitPublisher
{
    Task PublishAsync<T>(string exchange, string routingKey, T message,
        RabbitPublishProperties? properties = null, CancellationToken cancellationToken = default);
}

public sealed class RabbitPublishProperties
{
    public string? MessageId { get; init; }
    public string? CorrelationId { get; init; }
    public bool Persistent { get; init; } = true;
    public bool Mandatory { get; init; }
    public IReadOnlyDictionary<string, object?>? Headers { get; init; }
}

public enum RabbitPublishFailure { Nacked, Returned, ChannelClosed, ConnectionLost, Timeout }

public sealed class RabbitPublishException : Exception
{
    public RabbitPublishFailure Reason { get; }
    public string Exchange { get; }
    public string RoutingKey { get; }
    public string MessageId { get; }
    public ushort? ReplyCode { get; }
    public string? ReplyText { get; }

    // public, so apps can throw it from test doubles of IRabbitPublisher
    public RabbitPublishException(RabbitPublishFailure reason, string exchange, string routingKey, string messageId,
        ushort? replyCode = null, string? replyText = null, Exception? innerException = null);
}

// RPC client, until #75
public Task<RabbitMqRpcClient> CreateClientAsync(string requestQueueName, string? clientNameSuffix = null,
    bool disposeResponseQueue = true, JsonSerializerOptions? jsonSerializerOptions = null,
    string requestExchange = "", CancellationToken cancellationToken = default);
```

Configuration:

```json
{
  "RabbitMQ": {
    "ConnectionUri": "amqps://app:secret@rabbit.example.com:5671/prod",
    "ClientName": "orders-api-1",
    "PublishTimeout": "00:00:10"
  }
}
```

`RabbitMqRpcClient.DefaultJsonSerializerOptions` returns `RabbitMQJson.DefaultOptions` until
[#75](https://github.com/curiosus-dev/Curiosus.Utils/issues/75) removes the class.

## Edge cases and failure modes

- Missing exchange: the broker closes the channel, which also fails the other publishes in flight on it with
  `ChannelClosed`. The pool spreads publishes over several channels to limit this, and the README says to declare
  exchanges at startup.
- Non-mandatory unroutable message: the broker confirms and drops it; documented next to `Mandatory`.
- Connection blocked by the broker (memory or disk alarm): publishes wait and fail with `Timeout`.
- Retry after `ChannelClosed`, `ConnectionLost` or `Timeout`: the caller may retry; the message can then arrive twice,
  so consumers must be idempotent (documented).
- Invalid options (no URI, a relative URI, `http://`, user info with two `:`, `ClientName` with a space): the host fails
  to start with the options validation error, without the URI in it.
- Serializer throws: the exception propagates, nothing is published.
- Publish after the service provider is disposed: `ObjectDisposedException`.

## Test plan

- Unit: options validation (relative URI, scheme, host, bad user info without the password in the message,
  `ClientName` pattern and default, `PublishTimeout`, `PublisherChannelCount`); default serializer round trip, content
  type, unsupported content type; `RabbitMQJson.DefaultOptions` against recorded payloads of the current RPC client;
  `AddCuriosusRabbitMQ` registrations and a custom serializer kept; `amqps` policy errors reset to `None`.
- Integration (Testcontainers.RabbitMq, `rabbitmq` image, multi-arch): every acceptance scenario above; message
  properties and headers on the received message; caller cancellation while other publishes wait; connection and
  channel counts with `rabbitmqctl list_connections` / `list_channels`; recovery with
  `rabbitmqctl close_all_connections`; timeout with `docker pause` of the container; the existing RPC client tests
  with `ConnectionUri` and `requestExchange`.

## Rollout

- All issues of milestone [M5](https://github.com/curiosus-dev/Curiosus.Utils/milestone/5) (#109 parts 1–3, #75,
  #77, #78, #79) are released in one version (owner): `Curiosus.RabbitMQ` 4.0.0 and the RequestProcessing packages
  come out when all of them are done. Until then each merged part is released as a pre-release from `main`, starting
  with `## [4.0.0-alpha.1]` for this part, when the owner releases the alphas.
- So the breaking changes land in the first alpha (owner, review of this spec): `RabbitMQOptions` is bound from
  configuration and has `ConnectionUri` and `ClientName` instead of `HostName`/`Port`/`UserName`/`Password`/
  `ExchangeName`; `AddRabbitMQRPC(options)` becomes `AddCuriosusRabbitMQ(section)`; `ExchangeName` of the RPC client
  becomes the `requestExchange` argument; RabbitMQ.Client 7.2.1 is the minimum version. The rest of the RPC client API
  changes with [#75](https://github.com/curiosus-dev/Curiosus.Utils/issues/75), in a later alpha of the same version.
  The migration page gets the section of this part.
- `AddCuriosusOptions` is unreleased in `Curiosus.Configuration`: that package is released in the same release PR or
  before it.
- `Curiosus.RequestProcessing.RabbitMQ` keeps its own options and connection until
  [#77](https://github.com/curiosus-dev/Curiosus.Utils/issues/77); it builds against RabbitMQ.Client 7.2.1.
