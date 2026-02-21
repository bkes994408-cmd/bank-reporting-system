using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using BankReporting.Api.DTOs;
using BankReporting.Api.Models;
using BankReporting.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BankReporting.Tests;

public class ExcelParsingServiceTests
{
    private readonly ExcelParsingService _service = new();

    [Fact]
    public async Task ParseAsync_WhenFileIsNull_ReturnsMissingFile()
    {
        var result = await _service.ParseAsync("AI330", null);

        Assert.Equal(ParsingErrorCodes.MissingFile, result.Code);
    }

    [Fact]
    public async Task ParseAsync_WhenExtensionInvalid_ReturnsInvalidFileType()
    {
        var file = CreateFormFile(new byte[] { 1, 2, 3 }, "bad.csv");

        var result = await _service.ParseAsync("AI330", file);

        Assert.Equal(ParsingErrorCodes.InvalidFileType, result.Code);
    }

    [Fact]
    public async Task ParseAsync_WhenWorkbookValid_ReturnsParsedPayload()
    {
        var bytes = BuildSimpleXlsx();
        var file = CreateFormFile(bytes, "test.xlsx");

        var result = await _service.ParseAsync("AI330", file);

        Assert.Equal("0000", result.Code);
        Assert.NotNull(result.Payload);
        Assert.Equal("AI330", result.Payload!.ReportId);
        Assert.Equal("SheetA", result.Payload.SheetName);
        Assert.Equal(new[] { "欄位一", "欄位二" }, result.Payload.Headers);
        Assert.Single(result.Payload.Rows);
        Assert.Equal("值1", result.Payload.Rows[0]["欄位一"]);
        Assert.Equal("值2", result.Payload.Rows[0]["欄位二"]);
    }

    [Fact]
    public async Task ParseAsync_WhenNotZip_ReturnsInvalidWorkbook()
    {
        var file = CreateFormFile(Encoding.UTF8.GetBytes("not-zip"), "test.xlsx");

        var result = await _service.ParseAsync("AI330", file);

        Assert.Equal(ParsingErrorCodes.InvalidWorkbook, result.Code);
    }

    private static IFormFile CreateFormFile(byte[] bytes, string fileName)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "UploadFile", fileName);
    }

    private static byte[] BuildSimpleXlsx()
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheets><sheet name="SheetA" sheetId="1" r:id="rId1" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"/></sheets>
                </workbook>
                """);

            WriteEntry(zip, "xl/sharedStrings.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="4" uniqueCount="4">
                  <si><t>欄位一</t></si>
                  <si><t>欄位二</t></si>
                  <si><t>值1</t></si>
                  <si><t>值2</t></si>
                </sst>
                """);

            WriteEntry(zip, "xl/worksheets/sheet1.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="s"><v>0</v></c>
                      <c r="B1" t="s"><v>1</v></c>
                    </row>
                    <row r="2">
                      <c r="A2" t="s"><v>2</v></c>
                      <c r="B2" t="s"><v>3</v></c>
                    </row>
                  </sheetData>
                </worksheet>
                """);
        }

        return memory.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path);
        using var stream = new StreamWriter(entry.Open(), Encoding.UTF8);
        stream.Write(content.Trim());
    }
}

public class AgentServiceTests
{
    [Fact]
    public async Task DeclareAsync_WhenApiReturnsNullBody_ReturnsFallbackMessage()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));
        var service = BuildService(handler);

        var result = await service.DeclareAsync(new DeclareRequest { RequestId = "r1" });

        Assert.Equal("5000", result.Code);
        Assert.Equal("上傳失敗", result.Msg);
    }

    [Fact]
    public async Task GetDeclareResultAsync_WhenApiReturnsNullBody_ReturnsFallbackMessage()
    {
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", Encoding.UTF8, "application/json")
            }));
        var service = BuildService(handler);

        var result = await service.GetDeclareResultAsync(new DeclareResultRequest { RequestId = "r1" });

        Assert.Equal("5000", result.Code);
        Assert.Equal("查詢失敗", result.Msg);
    }

    [Fact]
    public async Task GetDeclareResultAsync_CallsExpectedEndpointPath()
    {
        Uri? capturedUri = null;
        var payload = JsonSerializer.Serialize(new ApiResponse<ReportDeclarationResult>
        {
            Code = "0000",
            Msg = "ok",
            Payload = new ReportDeclarationResult { Status = "SUCCESS" }
        });

        var handler = new StubHttpMessageHandler(request =>
        {
            capturedUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        });
        var service = BuildService(handler);

        var result = await service.GetDeclareResultAsync(new DeclareResultRequest { RequestId = "r1" });

        Assert.Equal("0000", result.Code);
        Assert.NotNull(capturedUri);
        Assert.Equal("/APBSA/agent-api/declare/result/v1", capturedUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetAgentInfoAsync_WhenRequestThrows_ReturnsExceptionMessage()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("network down"));
        var service = BuildService(handler);

        var result = await service.GetAgentInfoAsync();

        Assert.Equal("5000", result.Code);
        Assert.Contains("network down", result.Msg);
    }

    [Fact]
    public async Task ValidateKeysAsync_WhenApiReturnsPayload_ReturnsSuccessCode()
    {
        var payload = JsonSerializer.Serialize(new ApiResponse<object> { Code = "0000", Msg = "ok" });
        var handler = new StubHttpMessageHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            }));

        var service = BuildService(handler);
        var result = await service.ValidateKeysAsync();

        Assert.Equal("0000", result.Code);
    }

    private static AgentService BuildService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentSettings:BaseUrl"] = "https://agent.local/APBSA"
            })
            .Build();

        return new AgentService(httpClient, config);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request);
    }
}
