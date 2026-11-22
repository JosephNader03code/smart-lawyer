import {
  ChartPie, Microscope, History, TriangleAlert, Copy, SearchCheck, Swords, Gavel,
  FolderClosed, Database, Gem, type LucideIcon,
} from "lucide-react";

export type NavItem = { href: string; label: string; icon: LucideIcon; soon?: boolean };

export const navItems: NavItem[] = [
  { href: "/dashboard", label: "لوحة التحكم", icon: ChartPie },
  { href: "/case-analysis", label: "تحليل القضايا", icon: Microscope },
  { href: "/timeline", label: "تحليل الخط الزمني", icon: History },
  { href: "/contradictions", label: "كاشف التناقضات", icon: TriangleAlert },
  { href: "/similar-cases", label: "القضايا المشابهة", icon: Copy },
  { href: "/research", label: "البحث القانوني", icon: SearchCheck },
  { href: "/strategy", label: "استراتيجية القضية", icon: Swords },
  { href: "/simulator", label: "محاكي المحاكمة", icon: Gavel, soon: true },
  { href: "/documents", label: "مركز المستندات", icon: FolderClosed },
  { href: "/knowledge-base", label: "قاعدة معرفة المكتب", icon: Database, soon: true },
  { href: "/pricing", label: "باقات الاشتراك", icon: Gem },
];
