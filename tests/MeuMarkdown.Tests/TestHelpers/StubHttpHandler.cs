using System.Net;
using System.Net.Http;

namespace MeuMarkdown.Tests.TestHelpers;

/// <summary>
/// HttpMessageHandler de testes que devolve respostas configuradas por URL.
/// Use Map(url, response) pra registrar respostas; SendAsync casa por StartsWith.
/// </summary>
public sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _routes = new();

    public StubHttpHandler Map(string urlPrefix, HttpResponseMessage response)
    {
        _routes[urlPrefix] = _ => response;
        return this;
    }

    public StubHttpHandler Map(string urlPrefix, Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _routes[urlPrefix] = responder;
        return this;
    }

    public int RequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestCount++;
        cancellationToken.ThrowIfCancellationRequested();

        var url = request.RequestUri?.ToString() ?? "";
        foreach (var (prefix, responder) in _routes)
        {
            if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(responder(request));
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent($"No stub for {url}")
        });
    }
}
