using NpgsqlTypes;

namespace SmartLawyer.Infrastructure.Persistence.Entities;

// ===== بيانات قانونية عامة (مش خاصة بمكتب) =====

public sealed class Law
{
    public int Id { get; set; }
    public required string Title { get; set; }          // "القانون المدني"
    public string? Number { get; set; }                  // "131"
    public int? Year { get; set; }                       // 1948
    public string? Source { get; set; }                  // مصدر النص (الجريدة الرسمية، ...)
    public List<Article> Articles { get; set; } = [];
}

public sealed class Article
{
    public int Id { get; set; }
    public int LawId { get; set; }
    public Law Law { get; set; } = null!;
    public required string Number { get; set; }          // "147" أو "18 مكرر (أ)"
    public int SortOrder { get; set; }
    public List<ArticleVersion> Versions { get; set; } = [];
}

/// <summary>نص المادة في فترة سريان معينة. ValidTo = آخر يوم سريان (شامل)، null = سارية حالياً.</summary>
public sealed class ArticleVersion
{
    public int Id { get; set; }
    public int ArticleId { get; set; }
    public Article Article { get; set; } = null!;
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public required string Text { get; set; }
    public required string SearchText { get; set; }      // النص بعد التوحيد
    public NpgsqlTsVector SearchVector { get; set; } = null!;
}

public enum Court
{
    Cassation = 1,               // محكمة النقض
    SupremeConstitutional = 2,   // المحكمة الدستورية العليا
    SupremeAdministrative = 3,   // المحكمة الإدارية العليا
}

public sealed class Ruling
{
    public int Id { get; set; }
    public Court Court { get; set; }
    public int AppealNumber { get; set; }
    public int JudicialYear { get; set; }
    public DateOnly? SessionDate { get; set; }
    public string? Chamber { get; set; }                 // الدائرة (مدني، تجاري، جنائي...)
    public string? Principle { get; set; }               // المبدأ القانوني
    public required string Text { get; set; }
    public required string SearchText { get; set; }
    public NpgsqlTsVector SearchVector { get; set; } = null!;
    public string? Source { get; set; }
}

/// <summary>حكم بيستند لمادة أو لحكم تاني.</summary>
public sealed class Citation
{
    public int Id { get; set; }
    public int RulingId { get; set; }
    public Ruling Ruling { get; set; } = null!;
    public int? ArticleId { get; set; }
    public Article? Article { get; set; }
    public int? CitedRulingId { get; set; }
    public Ruling? CitedRuling { get; set; }
}
