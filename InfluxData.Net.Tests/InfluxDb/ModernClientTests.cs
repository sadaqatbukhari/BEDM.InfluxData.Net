using System;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.InfluxDb;
using InfluxDB.Client;
using Xunit;

namespace InfluxData.Net.Tests.InfluxDb
{
    public class ModernClientTests
    {
        [Theory]
        [InlineData(InfluxDbVersion.Latest)]
        [InlineData(InfluxDbVersion.v_1_8)]
        [InlineData(InfluxDbVersion.v_1_12)]
        public void Flux_OnSupportedVersion_ExposesOfficialQueryApi(InfluxDbVersion version)
        {
            using var client = CreateClient(version);

            Assert.IsAssignableFrom<IInfluxDbClientModern>(client);
            Assert.IsAssignableFrom<IQueryApi>(client.Flux);
            Assert.Same(client.Flux, client.Flux);
        }

        [Fact]
        public void Flux_OnOlderVersion_ThrowsWithoutAffectingEstablishedModules()
        {
            using var client = CreateClient(InfluxDbVersion.v_1_3);

            Assert.NotNull(client.Client);
            Assert.NotNull(client.Database);
            Assert.NotNull(client.Retention);
            Assert.NotNull(client.Delete);

            var exception = Assert.Throws<NotSupportedException>(() => client.Flux);
            Assert.Contains("require InfluxDB 1.8 or newer", exception.Message);
        }

        [Fact]
        public void Flux_AfterDispose_ThrowsObjectDisposedException()
        {
            var client = CreateClient(InfluxDbVersion.v_1_12);
            client.Dispose();

            Assert.Throws<ObjectDisposedException>(() => client.Flux);
        }

        private static InfluxDbClient CreateClient(InfluxDbVersion version)
        {
            return new InfluxDbClient(
                "http://localhost:8086/",
                "influx-user",
                "influx-password",
                version);
        }
    }
}
