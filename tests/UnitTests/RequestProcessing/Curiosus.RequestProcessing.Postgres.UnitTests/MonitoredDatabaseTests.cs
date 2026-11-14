using FluentAssertions;
using Xunit;

namespace Curiosus.RequestProcessing.Postgres.UnitTests;

public class MonitoredDatabaseTests
{
    private const string ConnectionString = "Host=db.local;Port=5433;Database=app;Username=app;Password=s3cr3t";

    [Fact]
    public void ToString_HostPortDatabase_NoPassword()
    {
        var database = new MonitoredDatabase(ConnectionString);

        database.ToString().Should().Be("db.local:5433/app").And.NotContain("s3cr3t");
    }

    [Fact]
    public void ToString_Unparsable_NoValue()
    {
        var database = new MonitoredDatabase("garbage s3cr3t");

        database.ToString().Should().NotContain("s3cr3t");
    }

    [Fact]
    public void Equals_ComparesFullConnectionString()
    {
        new MonitoredDatabase(ConnectionString).Should().Be(new MonitoredDatabase(ConnectionString));
        new MonitoredDatabase(ConnectionString).Should().NotBe(new MonitoredDatabase(ConnectionString + "x"));
    }
}
