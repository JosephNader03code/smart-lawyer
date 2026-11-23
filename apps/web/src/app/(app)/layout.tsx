import { AppShell } from "@/components/AppShell";
import { isSupabaseConfigured } from "@/lib/supabase/config";
import { createClient } from "@/lib/supabase/server";

// في النسخة الثابتة (GitHub Pages) مفيش سيرفر يقرا الـ cookies، فالمستخدم بيتجاب من المتصفح جوه AppShell.
const isStaticExport = process.env.STATIC_EXPORT === "1";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  let email: string | null = null;
  if (isSupabaseConfigured && !isStaticExport) {
    const supabase = await createClient();
    const { data } = await supabase.auth.getUser();
    email = data.user?.email ?? null;
  }
  return (
    <AppShell userEmail={email} authEnabled={isSupabaseConfigured}>
      {children}
    </AppShell>
  );
}
