"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Scale } from "lucide-react";
import { createClient } from "@/lib/supabase/client";
import { isSupabaseConfigured } from "@/lib/supabase/config";

const inputClass =
  "w-full rounded-lg border border-slate-800 bg-slate-900 px-3 py-2 text-sm text-slate-200 focus:border-blue-500 focus:outline-none";

export function AuthForm({ mode }: { mode: "login" | "signup" }) {
  const router = useRouter();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [message, setMessage] = useState<{ kind: "error" | "info"; text: string } | null>(null);
  const [loading, setLoading] = useState(false);
  const isLogin = mode === "login";

  async function onSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!isSupabaseConfigured) return;
    setLoading(true);
    setMessage(null);
    const supabase = createClient();
    if (isLogin) {
      const { error } = await supabase.auth.signInWithPassword({ email, password });
      if (error) setMessage({ kind: "error", text: "الإيميل أو كلمة السر غلط." });
      else {
        router.push("/dashboard");
        router.refresh();
      }
    } else {
      const { error } = await supabase.auth.signUp({
        email,
        password,
        options: { emailRedirectTo: `${window.location.origin}/auth/callback` },
      });
      if (error) setMessage({ kind: "error", text: error.message });
      else setMessage({ kind: "info", text: "بعتنالك إيميل تأكيد. افتحه واضغط على اللينك عشان تفعّل حسابك." });
    }
    setLoading(false);
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-slate-900 p-4">
      <form onSubmit={onSubmit} className="w-full max-w-sm space-y-5 rounded-2xl border border-slate-800 bg-navy-950 p-6 shadow-xl">
        <div className="flex items-center gap-3">
          <div className="flex h-9 w-9 items-center justify-center rounded-lg bg-blue-600 text-white">
            <Scale size={18} />
          </div>
          <div>
            <h1 className="text-lg font-bold text-white">المحامي الذكي</h1>
            <p className="text-xs text-slate-400">{isLogin ? "تسجيل الدخول" : "إنشاء حساب محامي"}</p>
          </div>
        </div>

        {!isSupabaseConfigured && (
          <p className="rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-xs text-amber-300">
            تسجيل الدخول مش متفعّل لسه (مفاتيح Supabase مش موجودة).{" "}
            <Link href="/dashboard" className="underline">ادخل في وضع التطوير</Link>
          </p>
        )}

        <label className="block space-y-1">
          <span className="text-xs font-semibold text-slate-300">الإيميل</span>
          <input type="email" required dir="ltr" value={email} onChange={(e) => setEmail(e.target.value)} className={inputClass} />
        </label>
        <label className="block space-y-1">
          <span className="text-xs font-semibold text-slate-300">كلمة السر</span>
          <input type="password" required minLength={8} dir="ltr" value={password} onChange={(e) => setPassword(e.target.value)} className={inputClass} />
        </label>

        {message && (
          <p className={`text-xs ${message.kind === "error" ? "text-red-400" : "text-emerald-400"}`}>{message.text}</p>
        )}

        <button
          type="submit"
          disabled={loading || !isSupabaseConfigured}
          className="w-full rounded-lg bg-blue-600 py-2.5 text-sm font-bold text-white hover:bg-blue-500 disabled:opacity-50"
        >
          {loading ? "لحظة..." : isLogin ? "دخول" : "إنشاء الحساب"}
        </button>

        <p className="text-center text-xs text-slate-400">
          {isLogin ? "معندكش حساب؟ " : "عندك حساب؟ "}
          <Link href={isLogin ? "/signup" : "/login"} className="text-blue-400 hover:underline">
            {isLogin ? "سجّل دلوقتي" : "ادخل"}
          </Link>
        </p>
      </form>
    </div>
  );
}
