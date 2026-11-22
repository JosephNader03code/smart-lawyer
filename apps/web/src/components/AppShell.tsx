"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";
import { Bell, Info, Menu, Plus, Scale, Search, X } from "lucide-react";
import { navItems } from "./nav";
import { SignOutButton } from "./SignOutButton";

type Props = { children: React.ReactNode; userEmail: string | null; authEnabled: boolean };

export function AppShell({ children, userEmail, authEnabled }: Props) {
  const pathname = usePathname();
  const [open, setOpen] = useState(false);

  const sidebar = (
    <aside className="flex h-full w-64 flex-col justify-between border-l border-slate-800 bg-navy-950">
      <div className="min-h-0 flex-1">
        <div className="flex h-16 items-center gap-3 border-b border-slate-800 px-6">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-blue-600 text-white shadow-lg shadow-blue-500/30">
            <Scale size={16} />
          </div>
          <span className="bg-gradient-to-r from-white via-slate-200 to-blue-400 bg-clip-text text-lg font-bold text-transparent">
            المحامي الذكي
          </span>
          <button className="mr-auto text-slate-400 lg:hidden" onClick={() => setOpen(false)} aria-label="إغلاق القائمة">
            <X size={18} />
          </button>
        </div>
        <nav className="custom-scrollbar max-h-[calc(100vh-140px)] space-y-1 overflow-y-auto p-3">
          {navItems.map(({ href, label, icon: Icon, soon }) => {
            const active = pathname === href;
            return (
              <Link
                key={href}
                href={href}
                onClick={() => setOpen(false)}
                className={`flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-all ${
                  active
                    ? "bg-blue-600 text-white shadow-md shadow-blue-600/20"
                    : "text-slate-400 hover:bg-slate-800/60 hover:text-slate-200"
                }`}
              >
                <Icon size={16} className="shrink-0" />
                {label}
                {soon && (
                  <span className="mr-auto rounded bg-slate-800 px-1.5 py-0.5 text-[10px] text-slate-400">قريباً</span>
                )}
              </Link>
            );
          })}
        </nav>
      </div>
      <div className="border-t border-slate-800 p-3">
        <div className="flex items-center gap-3 rounded-lg border border-slate-800 bg-slate-900/80 px-3 py-2">
          <div className="flex h-8 w-8 items-center justify-center rounded-full border border-blue-500/30 bg-blue-900 text-xs font-bold text-blue-300">
            {(userEmail ?? "م").charAt(0).toUpperCase()}
          </div>
          <div className="flex-1 overflow-hidden">
            <p className="truncate text-xs font-semibold text-slate-200">{userEmail ?? "وضع التطوير"}</p>
            <p className="truncate text-[10px] text-slate-400">{authEnabled ? "حساب محامي" : "تسجيل الدخول غير مفعّل"}</p>
          </div>
          {authEnabled && <SignOutButton />}
        </div>
      </div>
    </aside>
  );

  return (
    <div className="flex h-screen overflow-hidden bg-slate-900 text-slate-100">
      <div className="hidden shrink-0 lg:block">{sidebar}</div>

      {open && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <div className="absolute inset-0 bg-black/60" onClick={() => setOpen(false)} />
          <div className="absolute inset-y-0 right-0">{sidebar}</div>
        </div>
      )}

      <div className="flex h-screen min-w-0 flex-1 flex-col overflow-hidden">
        <header className="z-10 flex h-16 items-center justify-between gap-3 border-b border-slate-800 bg-navy-950/80 px-4 backdrop-blur-md sm:px-6">
          <button className="text-slate-300 lg:hidden" onClick={() => setOpen(true)} aria-label="فتح القائمة">
            <Menu size={20} />
          </button>
          <div className="relative hidden w-96 max-w-full md:block">
            <Search size={14} className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500" />
            <input
              type="text"
              disabled
              placeholder="البحث الشامل (قريباً)"
              className="w-full rounded-lg border border-slate-800 bg-slate-900 py-1.5 pl-4 pr-9 text-xs text-slate-200 placeholder-slate-500 focus:border-blue-500 focus:outline-none disabled:cursor-not-allowed"
            />
          </div>
          <div className="flex items-center gap-4">
            <button className="relative rounded-lg p-2 text-slate-400 hover:bg-slate-800/50 hover:text-slate-200" aria-label="الإشعارات">
              <Bell size={16} />
            </button>
            <div className="h-4 w-px bg-slate-800" />
            <Link
              href="/case-analysis"
              className="flex items-center gap-2 rounded-lg bg-blue-600 px-3 py-1.5 text-xs font-medium text-white shadow-md shadow-blue-600/20 hover:bg-blue-500"
            >
              <Plus size={12} /> تحليل قضية جديدة
            </Link>
          </div>
        </header>

        <div className="border-b border-amber-500/20 bg-amber-500/10 px-4 py-1.5 text-center text-[11px] text-amber-300 sm:px-6">
          نسخة تجريبية: البيانات المعروضة أمثلة توضيحية، والتحليل الحقيقي هيتفعّل في المراحل الجاية.
        </div>

        <main className="custom-scrollbar flex-1 space-y-6 overflow-y-auto p-4 sm:p-6">{children}</main>

        <footer className="shrink-0 border-t border-slate-800/80 bg-navy-950 px-4 py-2.5 text-center sm:px-6">
          <p className="text-[11px] font-medium text-slate-400">
            <Info size={12} className="ml-1 inline text-blue-400" />
            <strong>تنويه هام:</strong> يقدم &quot;المحامي الذكي&quot; تحليلاً قانونياً مساعِداً بالذكاء الاصطناعي، ويجب دائماً مراجعة
            النتائج وتدقيقها بواسطة محامٍ مؤهل.
          </p>
        </footer>
      </div>
    </div>
  );
}
