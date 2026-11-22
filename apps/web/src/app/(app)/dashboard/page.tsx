import Link from "next/link";
import { Copy, FilePlus2, Gavel, SearchCheck, TriangleAlert, Upload } from "lucide-react";
import { Card } from "@/components/ui";
import { activeCase, contradictions, formatDuration, timelineConflicts } from "@/lib/mock-data";

const quickActions = [
  { href: "/case-analysis", icon: FilePlus2, color: "text-blue-400", title: "تحليل قضية", sub: "استخراج الوقائع من المستندات" },
  { href: "/similar-cases", icon: Copy, color: "text-indigo-400", title: "قضايا مشابهة", sub: "مطابقة أحكام النقض" },
  { href: "/research", icon: SearchCheck, color: "text-cyan-400", title: "البحث القانوني", sub: "إجابات بالمصادر" },
  { href: "/simulator", icon: Gavel, color: "text-amber-400", title: "محاكي المحاكمة", sub: "قريباً" },
  { href: "/documents", icon: Upload, color: "text-emerald-400", title: "المستندات", sub: "أوراق القضية" },
];

export default function DashboardPage() {
  const issues = contradictions.length;
  return (
    <section className="space-y-6">
      <div className="flex flex-col items-start justify-between gap-4 rounded-2xl border border-blue-500/20 bg-gradient-to-l from-blue-900/40 via-navy-900 to-slate-900 p-6 shadow-xl md:flex-row md:items-center">
        <div className="space-y-1">
          <h1 className="text-2xl font-bold text-white">مرحباً بك، سيادة المستشار</h1>
          <p className="text-sm text-slate-400">
            عندك قضية واحدة قيد التحليل، وفيها <span className="font-medium text-amber-400">{issues} تعارضات</span> محتاجة مراجعتك.
          </p>
        </div>
        <Link href="/case-analysis" className="rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white shadow-lg shadow-blue-600/30 hover:bg-blue-500">
          تحليل قضية جديدة
        </Link>
      </div>

      <div className="grid grid-cols-2 gap-3 md:grid-cols-5">
        {quickActions.map(({ href, icon: Icon, color, title, sub }) => (
          <Link key={href} href={href} className="group rounded-xl border border-slate-800 bg-slate-800/50 p-4 text-right transition-all hover:border-blue-500/50 hover:bg-slate-800">
            <Icon size={22} className={`mb-2 ${color} transition-transform group-hover:scale-110`} />
            <p className="text-xs font-semibold text-slate-200">{title}</p>
            <p className="text-[10px] text-slate-500">{sub}</p>
          </Link>
        ))}
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <Card className="space-y-4 lg:col-span-2">
          <div className="flex items-center justify-between">
            <h3 className="text-sm font-bold text-slate-200">القضايا قيد التحليل</h3>
            <Link href="/case-analysis" className="text-xs text-blue-400 hover:underline">عرض الكل</Link>
          </div>
          <div className="flex flex-col items-start justify-between gap-3 rounded-lg border border-slate-800/80 bg-slate-900 p-4 sm:flex-row sm:items-center">
            <div className="space-y-1">
              <div className="flex flex-wrap items-center gap-2">
                <span className="rounded border border-blue-700/40 bg-blue-900/50 px-2 py-0.5 text-xs font-semibold text-blue-300">{activeCase.number}</span>
                <h4 className="text-sm font-semibold text-slate-200">{activeCase.title}</h4>
              </div>
              <p className="text-xs text-slate-400">{activeCase.type}</p>
            </div>
            <div className="flex items-center gap-4">
              <p className="text-xs text-amber-400">{issues} تعارضات مكتشفة</p>
              <Link href="/case-analysis" className="rounded border border-slate-700 bg-slate-800 px-3 py-1.5 text-xs text-slate-200 hover:bg-slate-700">مراجعة</Link>
            </div>
          </div>
        </Card>

        <Card className="space-y-4">
          <h3 className="flex items-center gap-2 text-sm font-bold text-slate-200">
            <TriangleAlert size={16} className="text-amber-500" /> تنبيهات التعارض
          </h3>
          <div className="space-y-3">
            {contradictions.map((c) => (
              <Link key={c.id} href="/contradictions" className="block space-y-1 rounded-lg border border-amber-500/20 bg-amber-500/10 p-3 hover:border-amber-500/40">
                <div className="flex items-center justify-between">
                  <span className="text-[10px] font-bold text-amber-400">{c.title}</span>
                  <span className="text-[10px] text-slate-500">{activeCase.number}</span>
                </div>
                <p className="text-xs font-medium text-slate-300">
                  {c.first.label} يتعارض مع {c.second.label} بفارق {formatDuration(c.differenceMinutes)}.
                </p>
              </Link>
            ))}
            {timelineConflicts.length > 0 && (
              <Link href="/timeline" className="block text-[11px] text-blue-400 hover:underline">عرض الخط الزمني</Link>
            )}
          </div>
        </Card>
      </div>
    </section>
  );
}
