// بيانات تجريبية متسقة لقضية واحدة، بتستخدمها كل الأقسام لحد ما الـ API الحقيقي يتوصل (M3/M4).
// كل رقم بيظهر في الواجهة محسوب من البيانات دي، مش مكتوب يدوي، عشان الأقسام ماتتعارضش مع بعض.

export type SourceRef = { document: string; location: string };

export type TimelineEvent = {
  id: string;
  date: string; // ISO yyyy-mm-dd
  title: string;
  description: string;
  source: SourceRef;
};

export type Contradiction = {
  id: string;
  kind: "time" | "date";
  title: string;
  first: { label: string; quote: string; highlight: string; source: SourceRef };
  second: { label: string; quote: string; highlight: string; source: SourceRef };
  /** الفرق بالدقايق، محسوب من القيم نفسها */
  differenceMinutes: number;
  explanation: string;
};

export type Finding = { type: "strength" | "weakness"; text: string; source: SourceRef };

export type CaseDocument = {
  name: string;
  kind: string;
  pages: number;
  status: "processed" | "processing" | "failed";
};

function minutesBetween(a: string, b: string): number {
  return Math.abs(new Date(b).getTime() - new Date(a).getTime()) / 60000;
}

export const activeCase = {
  number: "تجاري-2026-88",
  title: "شركة الأمل للتوريدات ضد المجموعة الوطنية للتجارة",
  type: "منازعات عقود تجارية",
  description:
    "تأخر المورد في تسليم الشحنة المحددة في عقد التوريد، مما تسبب في أضرار مالية للمدعي. يدعي المورد وجود قوة قاهرة.",
  summary:
    "تدور القضية حول الإخلال بعقد التوريد رقم 402. نقطة النزاع الرئيسية هي هل يجوز للمورد التمسك بالقوة القاهرة لتبرير التأخير، خصوصاً إن إخطار القوة القاهرة مؤرخ قبل الحادث نفسه.",
};

export const timeline: TimelineEvent[] = [
  {
    id: "contract",
    date: "2025-03-10",
    title: "توقيع عقد التوريد رقم 402",
    description: "توقيع العقد والاتفاق على جدول التسليم.",
    source: { document: "عقد التوريد", location: "صفحة 1" },
  },
  {
    id: "due",
    date: "2025-08-01",
    title: "الموعد التعاقدي للتسليم",
    description: "آخر موعد للتسليم حسب جدول العقد.",
    source: { document: "عقد التوريد", location: "البند 5" },
  },
  {
    id: "notice",
    date: "2025-08-13",
    title: "إخطار القوة القاهرة",
    description: "المورد يخطر المدعي بتعذر التسليم بسبب حادث للشحنة.",
    source: { document: "خطاب الإخطار", location: "صفحة 1" },
  },
  {
    id: "incident",
    date: "2025-08-15",
    title: "الحادث حسب محضر الشرطة",
    description: "تاريخ الحادث المثبت في محضر الشرطة.",
    source: { document: "محضر الشرطة", location: "صفحة 2" },
  },
  {
    id: "delivery",
    date: "2025-08-28",
    title: "التسليم الفعلي",
    description: "استلام الشحنة بمقر الشركة.",
    source: { document: "محضر الاستلام", location: "صفحة 1" },
  },
];

const notice = timeline.find((e) => e.id === "notice")!;
const incident = timeline.find((e) => e.id === "incident")!;

/** أحداث بتتعارض مع ترتيبها المنطقي: الإخطار عن حادث لازم يكون بعده. */
export const timelineConflicts = [
  {
    eventId: notice.id,
    hoursBefore: minutesBetween(notice.date, incident.date) / 60,
    message: `تاريخ الإخطار يسبق تاريخ الحادث المذكور في محضر الشرطة بـ ${
      minutesBetween(notice.date, incident.date) / 60
    } ساعة.`,
  },
];

export const contradictions: Contradiction[] = [
  {
    id: "delivery-time",
    kind: "time",
    title: "وقت التسليم",
    first: {
      label: "شهادة الشاهد",
      quote: "...أؤكد أن تسليم البضاعة تم في تمام {h} بمقر الشركة.",
      highlight: "الساعة 8:00 مساءً",
      source: { document: "شهادة الشاهد", location: "صفحة 3" },
    },
    second: {
      label: "محضر الاستلام الرسمي",
      quote: "...أفاد السائق بأن الوصول للموقع كان في تمام {h}.",
      highlight: "الساعة 10:30 مساءً",
      source: { document: "محضر الاستلام", location: "صفحة 1" },
    },
    differenceMinutes: minutesBetween("2025-08-28T20:00:00", "2025-08-28T22:30:00"),
    explanation:
      "الفرق بين أقوال الشاهد ومحضر الاستلام بيضعف حجية الشهادة في إثبات الالتزام بمواعيد التسليم.",
  },
  {
    id: "notice-date",
    kind: "date",
    title: "تاريخ الإخطار مقابل تاريخ الحادث",
    first: {
      label: "خطاب الإخطار",
      quote: "...نخطركم بتعذر التسليم نتيجة الحادث الذي وقع للشحنة، تحريراً في {h}.",
      highlight: "13 أغسطس 2025",
      source: notice.source,
    },
    second: {
      label: "محضر الشرطة",
      quote: "...وقع الحادث بتاريخ {h}.",
      highlight: "15 أغسطس 2025",
      source: incident.source,
    },
    differenceMinutes: minutesBetween(notice.date, incident.date),
    explanation: "الإخطار عن الحادث مكتوب قبل وقوعه، وده بيشكك في صحة الإخطار أو في تاريخه.",
  },
];

export const findings: Finding[] = [
  {
    type: "strength",
    text: "الإخطار عن القوة القاهرة مؤرخ قبل الحادث نفسه، وده بيضعف دفاع المورد.",
    source: notice.source,
  },
  {
    type: "strength",
    text: "التسليم الفعلي اتأخر 27 يوم عن الموعد التعاقدي.",
    source: { document: "محضر الاستلام", location: "صفحة 1" },
  },
  {
    type: "weakness",
    text: "مفيش مستندات رسمية لسه بتثبت حجم الأضرار المالية بدقة.",
    source: { document: "ملف القضية", location: "ملاحظات المحامي" },
  },
];

export const documents: CaseDocument[] = [
  { name: "عقد التوريد رقم 402.pdf", kind: "عقد", pages: 12, status: "processed" },
  { name: "خطاب الإخطار.pdf", kind: "مراسلة", pages: 1, status: "processed" },
  { name: "محضر الشرطة.pdf", kind: "محضر", pages: 4, status: "processed" },
  { name: "محضر الاستلام.pdf", kind: "محضر", pages: 2, status: "processed" },
  { name: "شهادة الشاهد.pdf", kind: "شهادة", pages: 5, status: "processing" },
];

export const strategy = {
  mainDefense: "عدم قبول الدعوى لعدم اللجوء للتحكيم المنصوص عليه في العقد.",
  mainDefenseSource: { document: "عقد التوريد", location: "البند 12" } as SourceRef,
};

export function formatArabicDate(iso: string): string {
  return new Intl.DateTimeFormat("ar-EG-u-nu-latn", {
    day: "numeric",
    month: "long",
    year: "numeric",
  }).format(new Date(iso));
}

export function formatDuration(minutes: number): string {
  if (minutes < 180) return `${minutes} دقيقة`;
  if (minutes < 60 * 24 * 3) return `${minutes / 60} ساعة`;
  return `${Math.round(minutes / (60 * 24))} يوم`;
}
