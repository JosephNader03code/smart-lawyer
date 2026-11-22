# المحامي الذكي (Smart Lawyer)

منصة ذكاء اصطناعي للمحامين المصريين. الخطة الكاملة والتعليمات في [PLAN.md](PLAN.md).

## المتطلبات
- .NET 10 SDK
- Docker (لتشغيل Postgres محلياً)
- Node.js 22 (للـ frontend، من M1)

## التشغيل محلياً

```bash
cp .env.example .env
```

```bash
docker compose up -d postgres
```

```bash
dotnet run --project src/SmartLawyer.Api
```

اختبر إن الـ API شغال: `GET http://localhost:5000/health`

### الـ Frontend

```bash
cd apps/web
```

```bash
npm install
```

```bash
npm run dev
```

افتح `http://localhost:3000`. من غير مفاتيح Supabase في `apps/web/.env.local` الموقع بيشتغل في وضع التطوير من غير تسجيل دخول.

## الاختبارات

```bash
dotnet test SmartLawyer.slnx
```
