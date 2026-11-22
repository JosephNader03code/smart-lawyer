import type { SourceRef } from "@/lib/mock-data";

export function PageHeader({ title, subtitle, action }: { title: string; subtitle?: string; action?: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-3 border-b border-slate-800 pb-4 sm:flex-row sm:items-center sm:justify-between">
      <div>
        <h1 className="text-xl font-bold text-white">{title}</h1>
        {subtitle && <p className="text-xs text-slate-400">{subtitle}</p>}
      </div>
      {action}
    </div>
  );
}

export function Card({ children, className = "" }: { children: React.ReactNode; className?: string }) {
  return <div className={`rounded-xl border border-slate-800 bg-navy-950/60 p-5 ${className}`}>{children}</div>;
}

export function Source({ source }: { source: SourceRef }) {
  return (
    <span className="text-[10px] text-blue-400">
      المصدر: {source.document} — {source.location}
    </span>
  );
}

export function ComingSoon({ milestone, children }: { milestone: string; children: React.ReactNode }) {
  return (
    <div className="rounded-xl border border-dashed border-slate-700 bg-slate-900/40 p-6 text-center">
      <p className="text-sm font-semibold text-slate-300">قريباً ({milestone})</p>
      <p className="mt-1 text-xs text-slate-500">{children}</p>
    </div>
  );
}
