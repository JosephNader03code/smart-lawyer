import { Card, PageHeader, Source } from "@/components/ui";
import { contradictions, formatDuration } from "@/lib/mock-data";

function Quote({ text, highlight }: { text: string; highlight: string }) {
  const [before, after] = text.split("{h}");
  return (
    <p className="rounded border border-slate-800 bg-slate-900 p-3 text-xs italic text-slate-300">
      &quot;{before}
      <mark className="rounded bg-amber-500/30 px-1 text-amber-200">{highlight}</mark>
      {after}&quot;
    </p>
  );
}

export default function ContradictionsPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="كاشف التناقضات" subtitle="مقارنة المستندات والشهادات تلقائياً لإبراز الاختلافات الجوهرية." />
      {contradictions.map((c) => (
        <div key={c.id} className="space-y-3">
          <h2 className="text-sm font-bold text-amber-400">
            {c.title} — فارق {formatDuration(c.differenceMinutes)}
          </h2>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            {[c.first, c.second].map((side) => (
              <Card key={side.label} className="space-y-3">
                <span className="block border-b border-slate-800 pb-1 text-xs font-bold text-slate-300">{side.label}</span>
                <Quote text={side.quote} highlight={side.highlight} />
                <Source source={side.source} />
              </Card>
            ))}
          </div>
          <Card className="space-y-1 bg-navy-950">
            <h4 className="text-xs font-bold text-blue-400">تفسير التناقض</h4>
            <p className="text-xs leading-relaxed text-slate-300">{c.explanation}</p>
          </Card>
        </div>
      ))}
    </section>
  );
}
