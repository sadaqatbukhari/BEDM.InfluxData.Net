using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using InfluxData.Net.Common.Enums;
using InfluxData.Net.Common.RequestClients;
using InfluxData.Net.InfluxDb;
using InfluxData.Net.InfluxDb.Infrastructure;
using Xunit;

namespace InfluxData.Net.Tests.InfluxDb
{
    public class InfluxDbClientDisposalTests
    {
        [Fact]
        public void Client_ImplementsSynchronousAndAsynchronousDisposal()
        {
            using var client = CreateClient();

            Assert.IsAssignableFrom<IDisposable>(client);
            Assert.IsAssignableFrom<IAsyncDisposable>(client);
        }

        [Fact]
        public async Task DisposeAsync_DisposesInternallyCreatedHttpClient()
        {
            var client = CreateClient();
            var requestClient = Assert.IsAssignableFrom<RequestClientBase>(client.RequestClient);
            var ownedHttpClient = requestClient.Configuration.HttpClient;

            await client.DisposeAsync();

            await Assert.ThrowsAsync<ObjectDisposedException>(
                () => ownedHttpClient.GetAsync("http://localhost/"));
        }

        [Fact]
        public async Task Dispose_DoesNotDisposeSuppliedHttpClient()
        {
            var handler = new TrackingHttpMessageHandler();
            using var suppliedHttpClient = new HttpClient(handler);
            var client = CreateClient(suppliedHttpClient);

            client.Dispose();

            Assert.False(handler.IsDisposed);
            using var response = await suppliedHttpClient.GetAsync("http://localhost/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public void Dispose_DoesNotDisposeHttpClientFromSuppliedConfiguration()
        {
            var handler = new TrackingHttpMessageHandler();
            using var suppliedHttpClient = new HttpClient(handler);
            var configuration = new InfluxDbClientConfiguration(
                new Uri("http://localhost:8086/"),
                "influx-user",
                "influx-password",
                InfluxDbVersion.v_1_12,
                httpClient: suppliedHttpClient);
            var firstClient = new InfluxDbClient(configuration);
            var secondClient = new InfluxDbClient(configuration);

            firstClient.Dispose();
            secondClient.Dispose();

            Assert.False(handler.IsDisposed);
        }

        [Fact]
        public async Task DisposeAndDisposeAsync_AreIdempotent()
        {
            var client = CreateClient();

            client.Dispose();
            client.Dispose();
            await client.DisposeAsync();
        }

        private static InfluxDbClient CreateClient(HttpClient httpClient = null)
        {
            return new InfluxDbClient(
                "http://localhost:8086/",
                "influx-user",
                "influx-password",
                InfluxDbVersion.v_1_12,
                httpClient: httpClient);
        }

        private sealed class TrackingHttpMessageHandler : HttpMessageHandler
        {
            public bool IsDisposed { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            }

            protected override void Dispose(bool disposing)
            {
                IsDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
