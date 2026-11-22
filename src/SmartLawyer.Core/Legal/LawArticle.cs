namespace SmartLawyer.Core.Legal;

/// <summary>مادة مستخرجة من نص قانون. Number زي "147" أو "147 مكرر" أو "147 مكرر (أ)".</summary>
public sealed record LawArticle(string Number, string Text);
