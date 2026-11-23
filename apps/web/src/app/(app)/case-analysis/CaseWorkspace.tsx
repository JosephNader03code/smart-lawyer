"use client";

import { useCallback, useEffect, useState } from "react";
import { CloudUpload, FileText, RefreshCw, TriangleAlert } from "lucide-react";
import { Card } from "@/components/ui";
import {
  ApiError, createCase, getContradictions, getTimeline, listCases, listDocuments, reanalyzeDocument, uploadDocument,
  type CaseSummary, type ContradictionView, type DocumentSummary, type TimelineView,
} from "@/lib/api";

const inputClass =
  "w-full rounded-lg border border-slate-800 bg-slate-900 px-3 py-2 text-xs text-slate-200 focus:border-blue-500 focus:outline-none";

const kindLabel: Record<string, string> = {
  signing: "توقيع", delivery: "تسليم", notice: "إخطار", incident: "حادث", payment: "دفع", hearing: "جلسة", other: "حدث",
};

const statusLabel: Record<DocumentSummary["status"], { text: string; cls: string }> = {
  Processed: { text: "تم التحليل", cls: "text-emerald-400 border-emerald-500/20 bg-emerald-500/10" },
  Processing: { text: "جاري التحليل", cls: "text-amber-400 border-amber-500/20 bg-amber-500/10" },
  Uploaded: { text: "اترفع", cls: "text-slate-300 border-slate-700 bg-slate-800" },
  Failed: { text: "مااتحللش", cls: "text-red-400 border-red-500/20 bg-red-500/10" },
};

function formatDate(iso: string, hasTime: boolean) {
  const d = new Date(iso);
  const date = new Intl.DateTimeFormat("ar-EG-u-nu-latn", { day: "numeric", month: "long", year: "numeric" }).format(d);
  if (!hasTime) return date;
  return `${date} — ${d.getHours().toString().padStart(2, "0")}:${d.getMinutes().toString().padStart(2, "0")}`;
}

function message(err: unknown) {
  return err instanceof ApiError ? err.message : "مقدرناش نوصل للسيرفر.";
}

export function CaseWorkspace() {
  const [cases, setCases] = useState<CaseSummary[]>([]);
  const [selected, setSelected] = useState<string>("");
  const [title, setTitle] = useState("");
  const [docs, setDocs] = useState<DocumentSummary[]>([]);
  const [timeline, setTimeline] = useState<TimelineView | null>(null);
  const [contradictions, setContradictions] = useState<ContradictionView[]>([]);
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const refreshCase = useCallback(async (caseId: string) => {
    if (!caseId) return;
    const [d, t, c] = await Promise.all([listDocuments(caseId), getTimeline(caseId), getContradictions(caseId)]);
    setDocs(d);
    setTimeline(t);
    setContradictions(c);
  }, []);

  useEffect(() => {
    listCases()
      .then((list) => {
        setCases(list);
        if (list.length > 0) setSelected(list[0].id);
      })
      .catch((e) => setError(message(e)));
  }, []);

  useEffect(() => {
    if (!selected) return;
    let cancelled = false;
    Promise.all([listDocuments(selected), getTimeline(selected), getContradictions(selected)])
      .then(([d, t, c]) => {
        if (cancelled) return;
        setDocs(d);
        setTimeline(t);
        setContradictions(c);
      })
      .catch((e) => !cancelled && setError(message(e)));
    return () => {
      cancelled = true;
    };
  }, [selected]);

  async function run(label: string, action: () => Promise<unknown>) {
    setBusy(label);
    setError(null);
    try {
      await action();
    } catch (e) {
      setError(message(e));
    } finally {
      setBusy(null);
    }
  }

  const onCreate = (e: React.FormEvent) => {
    e.preventDefault();
    if (title.trim().length < 3) return;
    run("create", async () => {
      const c = await createCase(title.trim());
      setCases((list) => [c, ...list]);
      setSelected(c.id);
      setTitle("");
    });
  };

  const onUpload = (files: FileList | null) => {
    if (!files || !selected) return;
    run("upload", async () => {
      for (const file of Array.from(files)) {
        await uploadDocument(selected, file);
      }
      await refreshCase(selected);
    });
  };

  const conflictIds = new Set(timeline?.conflicts.flatMap((c) => c.eventIds) ?? []);

  return (
    <div className="space-y-6">
      {error && <p className="rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-xs text-red-300">{error}</p>}

      <Card className="space-y-4">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <label className="block">
            <span className="mb-1 block text-xs font-semibold text-slate-300">القضية</span>
            <select value={selected} onChange={(e) => setSelected(e.target.value)} className={inputClass}>
              {cases.length === 0 && <option value="">مفيش قضايا لسه</option>}
              {cases.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.number} — {c.title}
                </option>
              ))}
            </select>
          </label>
          <form onSubmit={onCreate} className="block">
            <span className="mb-1 block text-xs font-semibold text-slate-300">قضية جديدة</span>
            <div className="flex gap-2">
              <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="عنوان القضية" className={inputClass} />
              <button disabled={busy !== null || title.trim().length < 3} className="shrink-0 rounded-lg bg-blue-600 px-4 text-xs font-bold text-white disabled:opacity-50">
                إنشاء
              </button>
            </div>
          </form>
        </div>

        {selected && (
          <label className="block cursor-pointer rounded-xl border-2 border-dashed border-slate-800 bg-slate-900/40 p-6 text-center hover:border-blue-500/50">
            <CloudUpload size={30} className="mx-auto mb-2 text-blue-500/80" />
            <p className="text-xs font-semibold text-slate-300">
              {busy === "upload" ? "بيتحلل... ممكن ياخد دقيقة" : "اضغط لاختيار مستندات القضية"}
            </p>
            <p className="mt-1 text-[10px] text-slate-500">PDF فيه نص أو TXT، لحد 10 ميجا. الملف الأصلي مش بيتخزن، النص بس.</p>
            <input type="file" accept=".pdf,.txt" multiple className="hidden" disabled={busy !== null} onChange={(e) => onUpload(e.target.files)} />
          </label>
        )}
        <p className="text-[10px] text-slate-500">
          ماترفعش مستندات فيها بيانات موكلين حقيقيين غير بعد ما تتأكد من إعدادات الخصوصية لمزوّد الذكاء الاصطناعي.
        </p>
      </Card>

      {docs.length > 0 && (
        <Card className="space-y-2">
          <h3 className="text-xs font-bold text-slate-300">المستندات</h3>
          {docs.map((d) => (
            <div key={d.id} className="flex flex-col gap-2 rounded-lg border border-slate-800 bg-slate-900 p-3 text-xs sm:flex-row sm:items-center sm:justify-between">
              <div className="min-w-0 space-y-1">
                <p className="truncate text-slate-200">
                  <FileText size={14} className="ml-2 inline text-blue-400" />
                  {d.fileName}
                </p>
                <p className="text-[10px] text-slate-500">{d.events} حدث • {d.facts} واقعة</p>
                {d.error && <p className="text-[10px] text-amber-400">{d.error}</p>}
              </div>
              <div className="flex shrink-0 items-center gap-2">
                <span className={`rounded-full border px-2 py-0.5 ${statusLabel[d.status].cls}`}>{statusLabel[d.status].text}</span>
                {d.status === "Failed" && (
                  <button
                    onClick={() => run("re", async () => { await reanalyzeDocument(selected, d.id); await refreshCase(selected); })}
                    disabled={busy !== null}
                    className="rounded border border-slate-700 px-2 py-0.5 text-slate-300 disabled:opacity-50"
                    aria-label="إعادة التحليل"
                  >
                    <RefreshCw size={12} />
                  </button>
                )}
              </div>
            </div>
          ))}
        </Card>
      )}

      {timeline && timeline.events.length > 0 && (
        <Card className="space-y-4">
          <h3 className="text-xs font-bold text-slate-300">الخط الزمني</h3>
          {timeline.conflicts.map((c, i) => (
            <p key={i} className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-xs text-amber-300">
              <TriangleAlert size={12} className="ml-1 inline" /> {c.message}
            </p>
          ))}
          <ol className="relative mr-4 space-y-5 border-r-2 border-slate-800">
            {timeline.events.map((e) => {
              const conflict = conflictIds.has(e.id);
              return (
                <li key={e.id} className="relative pr-6">
                  <div className={`absolute -right-[9px] top-1 h-4 w-4 rounded-full border-4 border-slate-900 ${conflict ? "bg-amber-500" : "bg-blue-600"}`} />
                  <div className={`space-y-1 rounded-xl border p-3 ${conflict ? "border-amber-500/30 bg-amber-950/20" : "border-slate-800 bg-slate-900"}`}>
                    <span className={`text-xs font-bold ${conflict ? "text-amber-400" : "text-blue-400"}`}>
                      {formatDate(e.date, e.hasTime)} • {kindLabel[e.kind] ?? e.kind}
                    </span>
                    <h4 className="text-sm font-semibold text-slate-200">{e.title}</h4>
                    <p className="text-xs italic text-slate-400">«{e.quote}»</p>
                    {e.document && <span className="text-[10px] text-blue-400">المصدر: {e.document}</span>}
                  </div>
                </li>
              );
            })}
          </ol>
        </Card>
      )}

      {contradictions.length > 0 && (
        <Card className="space-y-4">
          <h3 className="text-xs font-bold text-slate-300">التناقضات بين المستندات</h3>
          {contradictions.map((c, i) => (
            <div key={i} className="space-y-2">
              <p className="text-xs font-bold text-amber-400">{c.explanation}</p>
              <div className="grid grid-cols-1 gap-3 md:grid-cols-2">
                {[c.first, c.second].map((side, j) => (
                  <div key={j} className="space-y-1 rounded-lg border border-slate-800 bg-slate-900 p-3 text-xs">
                    <span className="block font-bold text-slate-300">{side.document}</span>
                    <p className="italic text-slate-400">«{side.quote}»</p>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </Card>
      )}

      {selected && docs.length > 0 && timeline?.events.length === 0 && contradictions.length === 0 && (
        <p className="text-center text-xs text-slate-500">مالقيناش أحداث بتواريخ مكتوبة في المستندات دي لسه.</p>
      )}
    </div>
  );
}
