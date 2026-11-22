import { NextResponse, type NextRequest } from "next/server";
import { createClient } from "@/lib/supabase/server";

// بعد تأكيد الإيميل، Supabase بيرجّع المستخدم هنا ومعاه code نبدّله بجلسة.
export async function GET(request: NextRequest) {
  const code = request.nextUrl.searchParams.get("code");
  if (code) {
    const supabase = await createClient();
    await supabase.auth.exchangeCodeForSession(code);
  }
  return NextResponse.redirect(new URL("/dashboard", request.url));
}
