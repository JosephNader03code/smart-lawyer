import { Card, PageHeader, Source } from "@/components/ui";
import { formatArabicDate, timeline, timelineConflicts } from "@/lib/mock-data";

export default function TimelinePage() {
  return (
    <section className="space-y-6">
      <PageHeader title="تحليل الخط الزمني" subtitle="ترتيب أحداث القضية تلقائياً واكتشاف التعارض في التواريخ." />
      <Card className="p-6">
        <ol className="relative mr-4 space-y-8 border-r-2 border-slate-800">
          {timeline.map((event) => {
            const conflict = timelineConflicts.find((c) => c.eventId === event.id);
            return (
              <li key={event.id} className="relative pr-6">
                <div
                  className={`absolute -right-[9px] top-1 h-4 w-4 rounded-full border-4 border-slate-900 ${
                    conflict ? "animate-pulse bg-amber-500" : "bg-blue-600"
                  }`}
                />
                <div
                  className={`space-y-1 rounded-xl border p-4 ${
                    conflict ? "border-amber-500/30 bg-amber-950/20" : "border-slate-800 bg-slate-900"
                  }`}
                >
                  <span className={`text-xs font-bold ${conflict ? "text-amber-400" : "text-blue-400"}`}>
                    {formatArabicDate(event.date)}
                    {conflict && " • تعارض تواريخ"}
                  </span>
                  <h4 className={`text-sm font-semibold ${conflict ? "text-amber-200" : "text-slate-200"}`}>{event.title}</h4>
                  <p className="text-xs text-slate-400">{conflict ? conflict.message : event.description}</p>
                  <Source source={event.source} />
                </div>
              </li>
            );
          })}
        </ol>
      </Card>
    </section>
  );
}
