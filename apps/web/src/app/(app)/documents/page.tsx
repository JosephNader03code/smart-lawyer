import { FileText } from "lucide-react";
import { PageHeader } from "@/components/ui";
import { activeCase, documents } from "@/lib/mock-data";

const statusLabel = {
  processed: { text: "تمت المعالجة", cls: "text-emerald-400 bg-emerald-500/10 border-emerald-500/20" },
  processing: { text: "جاري المعالجة", cls: "text-amber-400 bg-amber-500/10 border-amber-500/20" },
  failed: { text: "فشلت المعالجة", cls: "text-red-400 bg-red-500/10 border-red-500/20" },
} as const;

export default function DocumentsPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="مركز المستندات" subtitle={`أوراق القضية ${activeCase.number} وحالة معالجتها.`} />
      <div className="overflow-x-auto rounded-xl border border-slate-800 bg-navy-950/60">
        <table className="w-full min-w-[480px] text-right text-xs">
          <thead className="border-b border-slate-800 text-slate-400">
            <tr>
              <th className="p-3 font-semibold">المستند</th>
              <th className="p-3 font-semibold">النوع</th>
              <th className="p-3 font-semibold">الصفحات</th>
              <th className="p-3 font-semibold">الحالة</th>
            </tr>
          </thead>
          <tbody>
            {documents.map((d) => (
              <tr key={d.name} className="border-b border-slate-800/60 last:border-0">
                <td className="p-3 text-slate-200">
                  <FileText size={14} className="ml-2 inline text-blue-400" />
                  {d.name}
                </td>
                <td className="p-3 text-slate-400">{d.kind}</td>
                <td className="p-3 text-slate-400">{d.pages}</td>
                <td className="p-3">
                  <span className={`rounded-full border px-2 py-0.5 ${statusLabel[d.status].cls}`}>{statusLabel[d.status].text}</span>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
