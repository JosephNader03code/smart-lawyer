import { Card, ComingSoon, PageHeader } from "@/components/ui";

export default function SimilarCasesPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="القضايا والسوابق المشابهة" subtitle="مطابقة وقائع قضيتك مع أحكام محكمة النقض." />
      <Card className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <h3 className="text-base font-bold text-white">مثال توضيحي لشكل النتيجة</h3>
          <span className="rounded border border-slate-700 bg-slate-800 px-2 py-0.5 text-xs text-slate-400">ليس حكماً حقيقياً</span>
        </div>
        <p className="text-xs text-slate-300">
          كل نتيجة هتظهر برقم الطعن والسنة القضائية وتاريخ الجلسة والمبدأ القانوني، ورابط للنص الكامل للحكم.
        </p>
      </Card>
      <ComingSoon milestone="M3">البحث في أحكام النقض الحقيقية هيتفعّل لما قاعدة البيانات القانونية تجهز.</ComingSoon>
    </section>
  );
}
