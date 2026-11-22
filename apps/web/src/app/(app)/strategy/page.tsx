import { Card, PageHeader, Source } from "@/components/ui";
import { strategy } from "@/lib/mock-data";

export default function StrategyPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="بناء استراتيجية الترافع" subtitle="تجميع الحجج والأدلة لتحديد خطة الترافع المناسبة." />
      <Card className="space-y-2 text-xs text-slate-300">
        <p>
          <strong>الدفع الرئيسي المقترح:</strong> {strategy.mainDefense}
        </p>
        <Source source={strategy.mainDefenseSource} />
      </Card>
    </section>
  );
}
