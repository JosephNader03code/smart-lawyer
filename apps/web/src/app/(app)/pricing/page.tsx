import { CircleCheck } from "lucide-react";
import { PageHeader } from "@/components/ui";

const plans = [
  {
    name: "مجاني",
    price: "0",
    period: "ج.م",
    color: "border-slate-800",
    title: "text-slate-300",
    features: ["تحليل قضيتين", "5 أسئلة بحث يومياً"],
  },
  {
    name: "المحامي المحترف",
    price: "499",
    period: "ج.م / شهرياً",
    color: "border-blue-500",
    title: "text-blue-400",
    features: ["تحليل قضايا (استخدام عادل)", "الخط الزمني وكاشف التناقضات", "بحث قانوني بالمصادر"],
  },
  {
    name: "المكاتب",
    price: "2,999",
    period: "ج.م / شهرياً حتى 5 مستخدمين",
    color: "border-indigo-500",
    title: "text-indigo-400",
    features: ["كل مزايا المحترف", "عمل جماعي على القضايا", "قاعدة معرفة خاصة (قريباً)"],
  },
  {
    name: "الشركات",
    price: "حسب الاحتياج",
    period: "",
    color: "border-purple-500",
    title: "text-purple-400",
    features: ["ربط بأنظمتكم", "خيارات أمان واستضافة متقدمة"],
  },
];

export default function PricingPage() {
  return (
    <section className="space-y-6">
      <PageHeader title="باقات الاشتراك والأسعار" subtitle="الأسعار مبدئية خلال فترة التجربة." />
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {plans.map((p) => (
          <div key={p.name} className={`space-y-3 rounded-xl border bg-navy-950 p-5 ${p.color}`}>
            <h3 className={`font-bold ${p.title}`}>{p.name}</h3>
            <p className="text-2xl font-bold text-white">
              {p.price}
              <span className="mr-1 text-xs font-normal text-slate-400">{p.period}</span>
            </p>
            <ul className="space-y-1.5 text-xs text-slate-300">
              {p.features.map((f) => (
                <li key={f} className="flex items-center gap-2">
                  <CircleCheck size={12} className="shrink-0 text-emerald-400" /> {f}
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </section>
  );
}
