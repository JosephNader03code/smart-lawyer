"use client";

import { createClient } from "@/lib/supabase/client";
import { isSupabaseConfigured } from "@/lib/supabase/config";

export const apiUrl = process.env.NEXT_PUBLIC_API_URL ?? "";

/** الميزات الحقيقية (البحث وتحليل القضايا) محتاجة الـ API وتسجيل الدخول الاتنين. */
export const isApiEnabled = apiUrl !== "" && isSupabaseConfigured;
export const isResearchEnabled = isApiEnabled;

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

async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const { data } = await createClient().auth.getSession();
  const token = data.session?.access_token;
  if (!token) throw new ApiError(401, "لازم تسجّل دخول الأول.");

  const headers = new Headers(init.headers);
  headers.set("Authorization", `Bearer ${token}`);
  if (init.body && !(init.body instanceof FormData)) headers.set("Content-Type", "application/json");

  const res = await fetch(`${apiUrl.replace(/\/$/, "")}${path}`, { ...init, headers });
  if (!res.ok) {
    const body = await res.json().catch(() => null);
    throw new ApiError(res.status, body?.error ?? "حصل خطأ. جرّب تاني.");
  }
  return res.json();
}

export function askResearch(question: string): Promise<ResearchResponse> {
  return apiFetch("/api/research/ask", { method: "POST", body: JSON.stringify({ question }) });
}

export type CaseSummary = { id: string; number: string; title: string; type: string | null; createdAt: string; documents: number };
export type DocumentSummary = {
  id: string; fileName: string; status: "Processing" | "Processed" | "Failed" | "Uploaded"; error: string | null; events: number; facts: number;
};
export type TimelineItem = {
  id: string; kind: string; title: string; date: string; hasTime: boolean; rawDateText: string; quote: string; document: string | null; inConflict: boolean;
};
export type TimelineView = { events: TimelineItem[]; conflicts: { eventIds: string[]; message: string }[] };
export type ClaimView = { subject: string; quote: string; time: string | null; amount: number | null; document: string };
export type ContradictionView = { kind: "time" | "amount"; explanation: string; difference: number; first: ClaimView; second: ClaimView };

export const listCases = () => apiFetch<CaseSummary[]>("/api/cases");
export const createCase = (title: string, type?: string) =>
  apiFetch<CaseSummary>("/api/cases", { method: "POST", body: JSON.stringify({ title, type }) });
export const listDocuments = (caseId: string) => apiFetch<DocumentSummary[]>(`/api/cases/${caseId}/documents`);
export function uploadDocument(caseId: string, file: File) {
  const form = new FormData();
  form.append("file", file);
  return apiFetch<DocumentSummary>(`/api/cases/${caseId}/documents`, { method: "POST", body: form });
}
export const reanalyzeDocument = (caseId: string, docId: string) =>
  apiFetch<DocumentSummary>(`/api/cases/${caseId}/documents/${docId}/reanalyze`, { method: "POST" });
export const getTimeline = (caseId: string) => apiFetch<TimelineView>(`/api/cases/${caseId}/timeline`);
export const getContradictions = (caseId: string) => apiFetch<ContradictionView[]>(`/api/cases/${caseId}/contradictions`);
