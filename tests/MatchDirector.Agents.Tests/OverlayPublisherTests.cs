using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;

namespace MatchDirector.Agents.Tests;

public class OverlayPublisherTests
{
    [Fact]
    public async Task Viewers_languages_are_read_from_the_hub()
    {
        Assert.Equal(["fr", "it"], await Publisher("""["fr","it"]""", "application/json").LanguagesAsync(CancellationToken.None));
    }

    [Fact]
    public async Task An_older_hub_serving_its_web_page_means_no_languages_this_time_rather_than_an_error()
    {
        // During a deployment the previous hub can still answer, and it serves index.html for paths it does not know.
        var languages = await Publisher("<!doctype html><html><body>Ninetyfold</body></html>", "text/html").LanguagesAsync(CancellationToken.None);
        Assert.Empty(languages);
    }

    [Fact]
    public async Task A_hub_that_is_down_means_no_languages()
    {
        Assert.Empty(await Publisher("", "text/plain", HttpStatusCode.ServiceUnavailable).LanguagesAsync(CancellationToken.None));
    }

    private static OverlayPublisher Publisher(string body, string contentType, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new HttpClient(new Answer(body, contentType, status)) { BaseAddress = new Uri("http://overlayhub") }, NullLogger<OverlayPublisher>.Instance);

    private sealed class Answer(string body, string contentType, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, contentType) });
    }
}
