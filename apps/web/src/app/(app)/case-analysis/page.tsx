import { AlignRight, CloudUpload, Scale, Sparkles, TriangleAlert } from "lucide-react";
import { Card, PageHeader, Source } from "@/components/ui";
import { activeCase, findings } from "@/lib/mock-data";
import { isApiEnabled } from "@/lib/api";
import { CaseWorkspace } from "./CaseWorkspace";

const inputClass =
  "w-full rounded-lg border border-slate-800 bg-slate-900 px-3 py-2 text-xs text-slate-200 focus:border-blue-500 focus:outline-none";

export default function CaseAnalysisPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="محرك تحليل القضايا" subtitle="ادخل بيانات القضية وارفع المستندات لاستخراج الوقائع وتحليلها." />
      {isApiEnabled ? <CaseWorkspace /> : <DemoAnalysis />}
    </section>
  );
}

function DemoAnalysis() {
  return (
    <>

      <Card className="space-y-4 p-6">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          <label className="block">
            <span className="mb-1 block text-xs font-semibold text-slate-300">عنوان القضية</span>
            <input type="text" defaultValue={activeCase.title} className={inputClass} />
          </label>
          <label className="block">
            <span className="mb-1 block text-xs font-semibold text-slate-300">نوع القضية</span>
            <select defaultValue={activeCase.type} className={inputClass}>
              <option>منازعات عقود تجارية</option>
              <option>ملكية فكرية وحماية أسرار</option>
              <option>قضايا مدنية وتعويضات</option>
              <option>قضايا عمل وعمال</option>
            </select>
          </label>
        </div>
        <label className="block">
          <span className="mb-1 block text-xs font-semibold text-slate-300">وصف القضية والملاحظات الأولية</span>
          <textarea rows={2} defaultValue={activeCase.description} className={`${inputClass} p-3`} />
        </label>
        <div>
          <span className="mb-1 block text-xs font-semibold text-slate-300">المستندات والمرفقات</span>
          <div className="rounded-xl border-2 border-dashed border-slate-800 bg-slate-900/40 p-6 text-center">
            <CloudUpload size={30} className="mx-auto mb-2 text-blue-500/80" />
            <p className="text-xs font-semibold text-slate-300">رفع المستندات هيتفعّل في المرحلة الرابعة (M4)</p>
            <p className="mt-1 text-[10px] text-slate-500">ملفات PDF</p>
          </div>
        </div>
        <a href="#analysis-results" className="flex w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-blue-600 to-indigo-600 py-3 text-sm font-bold text-white shadow-lg shadow-blue-600/20 hover:from-blue-500">
          <Sparkles size={16} /> عرض نتيجة التحليل التجريبية
        </a>
      </Card>

      <div id="analysis-results" className="scroll-mt-6 space-y-6">
        <div className="flex flex-col gap-2 rounded-xl border border-slate-700/50 bg-slate-800/40 p-4 sm:flex-row sm:items-center sm:justify-between">
          <span className="text-sm font-bold text-slate-200">نتيجة تحليل تجريبية</span>
          <span className="w-fit rounded-full border border-amber-500/30 bg-amber-500/10 px-2.5 py-1 text-xs font-medium text-amber-400">
            <TriangleAlert size={12} className="ml-1 inline" /> يتطلب مراجعة المحامي
          </span>
        </div>
        <div className="grid grid-cols-1 gap-6 md:grid-cols-2">
          <Card className="space-y-3">
            <h3 className="flex items-center gap-2 text-xs font-bold text-slate-300">
              <AlignRight size={14} className="text-blue-400" /> ملخص القضية
            </h3>
            <p className="text-xs leading-relaxed text-slate-300">{activeCase.summary}</p>
          </Card>
          <Card className="space-y-3">
            <h3 className="flex items-center gap-2 text-xs font-bold text-slate-300">
              <Scale size={14} className="text-blue-400" /> نقاط القوة والضعف
            </h3>
            <div className="space-y-2 text-xs">
              {findings.map((f) => (
                <div
                  key={f.text}
                  className={`space-y-1 rounded border p-2 ${
                    f.type === "strength"
                      ? "border-emerald-500/20 bg-emerald-500/10 text-emerald-300"
                      : "border-red-500/20 bg-red-500/10 text-red-300"
                  }`}
                >
                  <p>
                    <strong className={f.type === "strength" ? "text-emerald-400" : "text-red-400"}>
                      {f.type === "strength" ? "نقطة قوة: " : "نقطة ضعف: "}
                    </strong>
                    {f.text}
                  </p>
                  <Source source={f.source} />
                </div>
              ))}
            </div>
          </Card>
        </div>
      </div>
    </>
  );
}
