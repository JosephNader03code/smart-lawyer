export const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL ?? "";
export const supabaseKey = process.env.NEXT_PUBLIC_SUPABASE_PUBLISHABLE_KEY ?? "";

/** لو المفاتيح مش موجودة، الموقع بيشتغل في وضع التطوير من غير تسجيل دخول. */
export const isSupabaseConfigured = supabaseUrl !== "" && supabaseKey !== "";
