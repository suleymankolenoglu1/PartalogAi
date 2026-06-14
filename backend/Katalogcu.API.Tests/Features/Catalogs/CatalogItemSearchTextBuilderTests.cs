using System.Net;
using System.Text;
using Katalogcu.API.Services;
using Katalogcu.Application.Common.Exceptions;
using Katalogcu.Application.Common.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Katalogcu.API.Tests.Features.Catalogs;

public class CatalogItemSearchTextBuilderTests
{
    [Fact]
    public async Task BuildSearchTextsAsync_UsesPythonCanonicalBatchEndpoint()
    {
        using var httpClient = new HttpClient(new StubHandler(
            HttpStatusCode.OK,
            "[\"Katalog Parça Adı: Lower Knife | Uyumlu Makine: JUKI DDL-8700 | Parça Kodu: B2421-280-000\"]"))
        {
            BaseAddress = new Uri("http://partalog-ai")
        };
        var service = new PartalogAiService(httpClient, NullLogger<PartalogAiService>.Instance);

        var result = await service.BuildSearchTextsAsync([
            new IngestionSearchTextRequest(
                PartName: "Lower Knife",
                MachineBrandModel: "JUKI DDL-8700",
                MachineBrand: "JUKI",
                MachineModel: "DDL-8700",
                MachineGroup: "Lockstitch",
                Category: "Lockstitch",
                Description: "Cuts thread near needle",
                PartCode: "B2421-280-000",
                RefNo: "12",
                Dimensions: "M5 3x3",
                Mechanism: "Needle / cutting mechanism")
        ]);

        Assert.Single(result);
        Assert.Contains("Lower Knife", result[0]);
        Assert.Equal("/api/v1/ingestion/build-search-texts", StubHandler.LastRequestPath);
        Assert.Contains("\"part_name\":\"Lower Knife\"", StubHandler.LastRequestBody);
        Assert.Contains("\"machine_brand_model\":\"JUKI DDL-8700\"", StubHandler.LastRequestBody);
        Assert.Contains("\"ref_no\":\"12\"", StubHandler.LastRequestBody);
    }

    [Fact]
    public async Task BuildSearchTextsAsync_RejectsMismatchedBatchResponse()
    {
        using var httpClient = new HttpClient(new StubHandler(HttpStatusCode.OK, "[]"))
        {
            BaseAddress = new Uri("http://partalog-ai")
        };
        var service = new PartalogAiService(httpClient, NullLogger<PartalogAiService>.Instance);

        await Assert.ThrowsAsync<CatalogAiRetryableException>(() => service.BuildSearchTextsAsync([
            new IngestionSearchTextRequest(
                PartName: "Lower Knife",
                MachineBrandModel: "JUKI DDL-8700",
                MachineBrand: "JUKI",
                MachineModel: "DDL-8700",
                MachineGroup: "Lockstitch",
                Category: "Lockstitch",
                Description: "Cuts thread near needle",
                PartCode: "B2421-280-000",
                RefNo: "12",
                Dimensions: "M5 3x3",
                Mechanism: "Needle / cutting mechanism")
        ]));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public static string? LastRequestPath { get; private set; }
        public static string? LastRequestBody { get; private set; }

        public StubHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
            LastRequestPath = null;
            LastRequestBody = null;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestPath = request.RequestUri?.PathAndQuery;
            LastRequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            };
        }
    }
}
