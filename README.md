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

### استيراد القوانين والأحكام

```bash
export ConnectionStrings__Default="Host=localhost;Database=smartlawyer;Username=postgres;Password=postgres"
```

```bash
dotnet run --project src/SmartLawyer.Ingestion -- migrate
```

```bash
dotnet run --project src/SmartLawyer.Ingestion -- import-law --file civil.txt --title "القانون المدني" --number 131 --year 1948 --valid-from 1949-10-15
```

```bash
dotnet run --project src/SmartLawyer.Ingestion -- import-rulings --dir ./rulings --court cassation
```

- **ملف القانون:** كل مادة تبدأ بسطر لوحدها زي `مادة 1` أو `المادة (147)` أو `مادة 18 مكرر (أ)`. لو استوردت نفس القانون تاني بنص معدّل و`--valid-from` أحدث، النسخة القديمة بتتحفظ.
- **ملف الحكم (ملف لكل حكم):** أول سطر فيه `الطعن رقم 1234 لسنة 85 ق - جلسة 2016/3/14`، وممكن تضيف سطور `الدائرة: ...` و`المبدأ: ...`.

## الاختبارات

الـ integration tests محتاجة Postgres، وبتعمل database مؤقتة وتمسحها في الآخر:

```bash
export TEST_DATABASE_URL="Host=localhost;Username=postgres;Password=postgres"
```

```bash
dotnet test SmartLawyer.slnx
```
