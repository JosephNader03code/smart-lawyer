import { Card, ComingSoon, PageHeader } from "@/components/ui";

export default function SimulatorPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="محاكي المحاكمة" subtitle="اختبر حججك أمام محاكاة للقاضي ومحامي الخصم." />
      <Card className="space-y-1 border-r-4 border-r-amber-500">
        <span className="text-xs font-bold text-amber-400">مثال لسؤال القاضي:</span>
        <p className="text-xs font-medium text-slate-200">
          &quot;حضرة الدفاع، كيف تطلب التعويض عن الكسب الفائت دون تقديم تقرير محاسبي معتمد يثبت ذلك؟&quot;
        </p>
      </Card>
      <ComingSoon milestone="M7">المحاكي هيتبني بعد فترة التجربة، حسب تقييم المحامين.</ComingSoon>
    </section>
  );
}
