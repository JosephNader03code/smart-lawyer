"use client";

import { createClient } from "@/lib/supabase/client";
import { isSupabaseConfigured } from "@/lib/supabase/config";

export const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "";

/** البحث الحقيقي محتاج الـ API وتسجيل الدخول الاتنين. */
export const isResearchEnabled = apiUrl !== "" && isSupabaseConfigured;

export type ResearchSource = { key: string; title: string; text: string };

export type ResearchResponse = {
  answered: boolean;
  text: string;
  sources: ResearchSource[];
  cached: boolean;
  remainingToday: number;
};

export class ApiError extends Error {
  constructor(public status: number, message: string) {
    super(message);
  }
}

export async function askResearch(question: string): Promise<ResearchResponse> {
  const { data } = await createClient().auth.getSession();
  const token = data.session?.access_token;
  if (!token) throw new ApiError(401, "لازم تسجّل دخول الأول.");

  const res = await fetch(`${apiUrl.replace(/\/$/, "")}/api/research/ask`, {
    method: "POST",
    headers: { "Content-Type": "application/json", Authorization: `Bearer ${token}` },
    body: JSON.stringify({ question }),
  });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new ApiError(res.status, body?.error ?? "حصل خطأ. جرّب تاني.");
  }
  return res.json();
}
