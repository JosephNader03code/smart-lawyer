"use client";

import { useState } from "react";
import { Scale } from "lucide-react";
import { askResearch, ApiError, type ResearchResponse } from "@/lib/api";

type Turn = { question: string; response?: ResearchResponse; error?: string };

const CITATION = /\[\[(article|ruling):[^\]]+\]\]/g;

/** بيبدّل مفاتيح الاستشهاد [[...]] بأرقام [1] [2] بترتيب ظهورها في المصادر. */
function renderAnswer(text: string, sources: ResearchResponse["sources"]) {
  const parts: React.ReactNode[] = [];
  let last = 0;
  for (const match of text.matchAll(CITATION)) {
    const index = sources.findIndex((s) => s.key === match[0]);
    parts.push(text.slice(last, match.index));
    parts.push(
      <a key={match.index} href={`#source-${index}`} className="mx-0.5 rounded bg-blue-500/20 px-1 text-[10px] font-bold text-blue-300">
        [{index + 1}]
      </a>,
    );
    last = match.index! + match[0].length;
  }
  parts.push(text.slice(last));
  return parts;
}

export function ResearchChat() {
  const [turns, setTurns] = useState<Turn[]>([]);
  const [question, setQuestion] = useState("");
  const [loading, setLoading] = useState(false);

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    const q = question.trim();
    if (q.length < 5 || loading) return;
    setLoading(true);
    setQuestion("");
    setTurns((t) => [...t, { question: q }]);
    try {
      const response = await askResearch(q);
      setTurns((t) => t.map((turn, i) => (i === t.length - 1 ? { ...turn, response } : turn)));
    } catch (err) {
      const message = err instanceof ApiError ? err.message : "مقدرناش نوصل للسيرفر.";
      setTurns((t) => t.map((turn, i) => (i === t.length - 1 ? { ...turn, error: message } : turn)));
    } finally {
      setLoading(false);
    }
  }

  const remaining = [...turns].reverse().find((t) => t.response)?.response?.remainingToday;

  return (
    <div className="flex h-[560px] flex-col rounded-xl border border-slate-800 bg-navy-950/60">
      <div className="custom-scrollbar flex-1 space-y-5 overflow-y-auto p-4 sm:p-6">
        {turns.length === 0 && (
          <p className="text-center text-xs text-slate-500">
            اسأل سؤال قانوني، والإجابة هتتكتب من نصوص القوانين وأحكام النقض الموجودة في قاعدة البيانات بس، ومعاها مصادرها.
          </p>
        )}
        {turns.map((turn, i) => (
          <div key={i} className="space-y-3">
            <div className="flex justify-start">
              <div className="max-w-lg rounded-xl rounded-tl-none bg-blue-600 p-3 text-xs text-white shadow-md">{turn.question}</div>
            </div>
            <div className="flex max-w-3xl items-start gap-3">
              <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-white">
                <Scale size={14} />
              </div>
              <div className="min-w-0 flex-1 space-y-3 rounded-xl rounded-tr-none border border-slate-800 bg-slate-900 p-4 text-xs leading-relaxed text-slate-300">
                {!turn.response && !turn.error && <p className="animate-pulse text-slate-500">بيدوّر في القوانين والأحكام...</p>}
                {turn.error && <p className="text-red-400">{turn.error}</p>}
                {turn.response && (
                  <>
                    {!turn.response.answered && (
                      <p className="rounded border border-amber-500/30 bg-amber-500/10 p-2 text-amber-300">مفيش سند كافي</p>
                    )}
                    <p className="whitespace-pre-line">{renderAnswer(turn.response.text, turn.response.sources)}</p>
                    {turn.response.sources.length > 0 && (
                      <div className="space-y-2 border-t border-slate-800 pt-3">
                        <p className="text-[10px] font-bold text-blue-400">المصادر</p>
                        {turn.response.sources.map((s, si) => (
                          <details key={s.key} id={`source-${si}`} className="rounded border border-slate-800 bg-slate-950/60 p-2">
                            <summary className="cursor-pointer text-[11px] text-slate-200">
                              [{si + 1}] {s.title}
                            </summary>
                            <p className="mt-2 whitespace-pre-line text-[11px] text-slate-400">{s.text}</p>
                          </details>
                        ))}
                      </div>
                    )}
                    {turn.response.cached && <p className="text-[10px] text-slate-500">إجابة محفوظة من سؤال سابق (ماتحسبتش من حدك اليومي)</p>}
                  </>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>
      <form onSubmit={onSubmit} className="space-y-2 border-t border-slate-800 bg-slate-900/50 p-4">
        <div className="flex gap-3">
          <input
            type="text"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            maxLength={1000}
            placeholder="اكتب سؤالك القانوني هنا..."
            className="flex-1 rounded-xl border border-slate-800 bg-slate-900 px-4 py-2.5 text-xs text-slate-200 focus:border-blue-500 focus:outline-none"
          />
          <button disabled={loading || question.trim().length < 5} className="rounded-xl bg-blue-600 px-5 text-xs font-bold text-white disabled:opacity-50">
            إرسال
          </button>
        </div>
        <p className="text-[10px] text-slate-500">
          ماتكتبش أسماء موكلين أو بيانات تعرّف بيهم في السؤال.
          {remaining !== undefined && ` • باقي ${remaining} سؤال النهارده`}
        </p>
      </form>
    </div>
  );
}
