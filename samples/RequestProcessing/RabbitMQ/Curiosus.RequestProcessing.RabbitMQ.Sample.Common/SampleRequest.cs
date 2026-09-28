using System.Globalization;
using Curiosus.RequestProcessing;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.Common;

/// <summary>
/// Sample request.
/// </summary>
public class SampleRequest : IRequest
{
    /// <inheritdoc />
    public long Id { get; }

    /// <inheritdoc />
    public CultureInfo RequestCulture { get; }

    /// <summary>
    /// Sample data.
    /// </summary>
    public string SomeData { get; }

    /// <inheritdoc cref="SampleRequest"/>
    public SampleRequest(long id, CultureInfo requestCulture, string someData)
    {
        Id = id;
        RequestCulture = requestCulture;
        SomeData = someData;
    }
}
