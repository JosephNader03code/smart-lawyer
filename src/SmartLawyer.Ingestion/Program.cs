using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartLawyer.Core.Arabic;
using SmartLawyer.Core.Legal;
using SmartLawyer.Infrastructure;
using SmartLawyer.Infrastructure.Import;
using SmartLawyer.Infrastructure.Persistence;
using SmartLawyer.Infrastructure.Persistence.Entities;
using SmartLawyer.Infrastructure.Tenancy;
using UglyToad.PdfPig;

// أداة استيراد القوانين والأحكام. أمثلة:
//   dotnet run --project src/SmartLawyer.Ingestion -- migrate
//   dotnet run --project src/SmartLawyer.Ingestion -- import-law --file civil.txt --title "القانون المدني" --number 131 --year 1948 --valid-from 1949-10-15 [--complete true]
//   dotnet run --project src/SmartLawyer.Ingestion -- import-rulings --dir ./rulings --court cassation
Console.OutputEncoding = Encoding.UTF8;

// preview-law مش محتاج قاعدة بيانات: بيعرض نتيجة التقسيم عشان تراجعها قبل الاستيراد.
if (args.Length > 0 && args[0] == "preview-law")
{
    var o = ParseOptions(args.Skip(1).ToArray());
    var articles = LawTextSplitter.Split(await ReadTextAsync(Required(o, "file"), o.GetValueOrDefault("fix-kaf") == "true"));
    Console.WriteLine($"عدد المواد: {articles.Count}");
    var numeric = articles.Select(a => int.TryParse(a.Number, out var n) ? n : (int?)null).Where(n => n is not null).Select(n => n!.Value).ToList();
    if (numeric.Count > 0)
    {
        var missing = Enumerable.Range(numeric.Min(), numeric.Max() - numeric.Min() + 1).Except(numeric).ToList();
        Console.WriteLine($"المواد من {numeric.Min()} لـ {numeric.Max()}، الناقص: {(missing.Count == 0 ? "مفيش" : string.Join("، ", missing.Take(50)) + (missing.Count > 50 ? $" ... ({missing.Count})" : ""))}");
        var dupes = articles.GroupBy(a => a.Number).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Console.WriteLine($"المكرر: {(dupes.Count == 0 ? "مفيش" : string.Join("، ", dupes.Take(50)))}");
    }
    foreach (var a in articles.Take(int.Parse(o.GetValueOrDefault("show") ?? "3", CultureInfo.InvariantCulture)))
    {
        Console.WriteLine($"--- مادة {a.Number}\n{a.Text[..Math.Min(300, a.Text.Length)]}");
    }
    return 0;
}

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("حط ConnectionStrings__Default في الـ environment الأول.");
    return 1;
}
if (args.Length == 0)
{
    Console.Error.WriteLine("الأوامر: migrate | import-law | import-rulings");
    return 1;
}

var services = new ServiceCollection()
    .AddSingleton<ITenantContext>(new FixedTenantContext(null))
    .AddInfrastructure(connectionString)
    .BuildServiceProvider();
await using var scope = services.CreateAsyncScope();
var sp = scope.ServiceProvider;
var options = ParseOptions(args.Skip(1).ToArray());

try
{
    switch (args[0])
    {
        case "migrate":
            await sp.GetRequiredService<AppDbContext>().Database.MigrateAsync();
            Console.WriteLine("تم تحديث قاعدة البيانات.");
            return 0;

        case "import-law":
        {
            var text = await ReadTextAsync(Required(options, "file"), options.GetValueOrDefault("fix-kaf") == "true");
            var result = await sp.GetRequiredService<LegalImportService>().ImportLawAsync(new LawImportRequest(
                Title: Required(options, "title"),
                Number: options.GetValueOrDefault("number"),
                Year: options.TryGetValue("year", out var y) ? int.Parse(y, CultureInfo.InvariantCulture) : null,
                Source: options.GetValueOrDefault("source"),
                ValidFrom: DateOnly.ParseExact(Required(options, "valid-from"), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                Text: text,
                IsCompleteText: options.GetValueOrDefault("complete") == "true"));
            Console.WriteLine($"القانون #{result.LawId}: {result.ArticlesFound} مادة — جديدة {result.NewArticles}، نسخ معدلة {result.NewVersions}، بدون تغيير {result.Unchanged}، ملغية {result.Repealed}.");
            return 0;
        }

        case "import-rulings":
        {
            var court = Enum.Parse<Court>(options.GetValueOrDefault("court") ?? "cassation", ignoreCase: true);
            var importer = sp.GetRequiredService<LegalImportService>();
            int ok = 0, failed = 0;
            foreach (var file in Directory.EnumerateFiles(Required(options, "dir")).Where(IsSupported).Order())
            {
                try
                {
                    var r = await importer.ImportRulingAsync(new RulingImportRequest(court, await ReadTextAsync(file),
                        Source: options.GetValueOrDefault("source") ?? Path.GetFileName(file)));
                    Console.WriteLine($"{(r.Created ? "جديد" : "تحديث")}: {r.Reference} ← {Path.GetFileName(file)}");
                    ok++;
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine($"فشل {Path.GetFileName(file)}: {ex.Message}");
                    failed++;
                }
            }
            Console.WriteLine($"تم: {ok}، فشل: {failed}");
            return failed == 0 ? 0 : 2;
        }

        default:
            Console.Error.WriteLine($"أمر غير معروف: {args[0]}");
            return 1;
    }
}
catch (InvalidOperationException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

static bool IsSupported(string path) =>
    path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

// ملفات .txt مفضّلة. استخراج النص العربي من PDF ممكن يطلع الحروف بترتيب غلط حسب طريقة إنشاء الملف،
// فلازم تراجع عينة من الناتج قبل استيراد ملفات PDF كتير.
// --fix-kaf true: للملفات اللي الخط فيها كاتب "ك" كأنها "آ". الكلمات اللي اتسابت بـ "آ" بتتطبع للمراجعة.
static async Task<string> ReadTextAsync(string path, bool fixKaf = false)
{
    string raw;
    if (!path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
    {
        raw = await File.ReadAllTextAsync(path, Encoding.UTF8);
    }
    else
    {
        using var pdf = PdfDocument.Open(path);
        raw = string.Join("\n", pdf.GetPages().Select(p => p.Text));
    }
    // بيصلّح أشكال الحروف وعلامات الاتجاه اللي بتطلع من PDF، ومابيضرّش النص النضيف.
    var clean = ArabicPdfTextCleaner.Clean(raw);
    if (!fixKaf)
    {
        return clean;
    }
    var (fixedText, kept) = ArabicPdfTextCleaner.FixKafShownAsAlefMadda(clean);
    Console.WriteLine($"تصليح ك/آ: الكلمات اللي اتسابت بـ «آ» ({kept.Count}): {string.Join("، ", kept.Take(80))}");
    return fixedText;
}

static Dictionary<string, string> ParseOptions(string[] a)
{
    var result = new Dictionary<string, string>();
    for (var i = 0; i + 1 < a.Length; i += 2)
    {
        if (!a[i].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"متوقع اسم option يبدأ بـ -- بدل «{a[i]}»");
        }
        result[a[i][2..]] = a[i + 1];
    }
    return result;
}

static string Required(Dictionary<string, string> o, string name) =>
    o.TryGetValue(name, out var v) ? v : throw new InvalidOperationException($"ناقص --{name}");
