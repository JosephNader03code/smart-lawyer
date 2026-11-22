using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SmartLawyer.Infrastructure.Import;
using SmartLawyer.Infrastructure.Search;

namespace SmartLawyer.Api.Tests;

public class LegalEndpointsTests(DatabaseFixture fx) : IClassFixture<DatabaseFixture>
{
    private HttpClient CreateClient() => new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Default", fx.ConnectionString))
        .CreateClient();

    [Fact]
    public async Task SearchAndGetArticle_ThroughHttp()
    {
        int lawId;
        await using (var db = fx.CreateContext(null))
        {
            lawId = (await new LegalImportService(db).ImportLawAsync(new LawImportRequest(
                "قانون تجريبي للـ API", null, null, null, new DateOnly(2000, 1, 1),
                "مادة 1\nيلتزم المؤجر بصيانة العين المؤجرة.\nمادة 2\nيلتزم المستأجر بدفع الأجرة."))).LawId;
        }

        var client = CreateClient();
        var hits = await client.GetFromJsonAsync<List<ArticleHit>>("/api/legal/articles/search?q=" + Uri.EscapeDataString("الاجره"));
        Assert.Equal("2", Assert.Single(hits!).ArticleNumber);

        var article = await client.GetFromJsonAsync<ArticleHit>($"/api/legal/laws/{lawId}/articles/1?asOf=2024-01-01");
        Assert.Contains("صيانة", article!.Text);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/legal/laws/{lawId}/articles/99")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/legal/laws/{lawId}/articles/1?asOf=1-1-2024")).StatusCode);
    }
}
