import { Scale } from "lucide-react";
import { PageHeader } from "@/components/ui";
import { isResearchEnabled } from "@/lib/api";
import { ResearchChat } from "./ResearchChat";

export default function ResearchPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="المساعد الذكي للبحوث القانونية" subtitle="اسأل واحصل على إجابات مستندة لنصوص القانون وأحكام النقض." />
      {isResearchEnabled ? <ResearchChat /> : <ResearchPreview />}
    </section>
  );
}

function ResearchPreview() {
  return (
    <div className="flex h-[450px] flex-col rounded-xl border border-slate-800 bg-navy-950/60">
      <div className="custom-scrollbar flex-1 space-y-4 overflow-y-auto p-4 sm:p-6">
        <div className="flex justify-start">
          <div className="max-w-lg rounded-xl rounded-tl-none bg-blue-600 p-3 text-xs text-white shadow-md">
            ما هي الشروط الواجب توافرها للأخذ بدفاتر التجار كدليل إثبات أمام المحكمة؟
          </div>
        </div>
        <div className="flex max-w-2xl items-start gap-3">
          <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-blue-600 text-white">
            <Scale size={14} />
          </div>
          <div className="space-y-2 rounded-xl rounded-tr-none border border-slate-800 bg-slate-900 p-4 text-xs text-slate-300">
            <p>ده مثال لشكل الإجابة. الإجابة الحقيقية هتتكتب من نصوص القانون وأحكام النقض الموجودة في قاعدة البيانات، وكل نقطة هيبقى معاها مصدرها.</p>
            <div className="border-t border-slate-800 pt-2 text-[10px] text-slate-500">المصادر هتظهر هنا (رقم المادة / رقم الطعن والسنة)</div>
          </div>
        </div>
      </div>
      <div className="border-t border-slate-800 bg-slate-900/50 p-4 text-center text-[11px] text-slate-500">
        البحث الحقيقي بيتفعّل لما الـ API وتسجيل الدخول يتربطوا (NEXT_PUBLIC_API_URL ومفاتيح Supabase).
      </div>
    </div>
  );
}
