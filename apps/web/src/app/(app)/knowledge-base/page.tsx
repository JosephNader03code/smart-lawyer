import { ComingSoon, PageHeader } from "@/components/ui";

export default function KnowledgeBasePage() {
  return (
    <section className="space-y-6">
      <PageHeader title="قاعدة معرفة المكتب" subtitle="مساحة خاصة لمكتبك يقرأ فيها النظام صيغك ونماذجك السابقة بأمان." />
      <ComingSoon milestone="M7">متاحة في باقة المكاتب بعد فترة التجربة.</ComingSoon>
    </section>
  );
}
