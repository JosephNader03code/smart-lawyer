# المحامي الذكي — خطة البناء والتعليمات

منصة ذكاء اصطناعي للمحامين المصريين: بحث قانوني بالمصادر، تحليل قضايا، خط زمني، كاشف تناقضات، ولاحقاً محاكي محاكمة.
المرحلة الحالية: **تجربة (Pilot) مع 20 محامي بأقل تكلفة ممكنة** (هدف: أقل من 30 دولار في الشهر للـ 20 محامي).

---

## 1. المبادئ الأساسية (لازم تتطبق في كل سطر كود)

1. **أي حاجة ليها إجابة واحدة صح بتتحسب بخوارزم ثابت، مش بالـ AI.**
   التواريخ، المدد، مواعيد الطعن، الأرقام، المبالغ، هل المادة سارية في تاريخ معين — كلها كود عادي مع tests.
2. **الـ AI شغلته 3 حاجات بس:** استخراج معلومات من نص، فهم المعنى، وصياغة الإجابة.
3. **مفيش جملة قانونية من غير مصدر.** كل إجابة لازم تستشهد بمادة أو حكم موجود في قاعدة بياناتنا.
   الـ Citation Verifier بيرفض أي إجابة فيها مصدر مش موجود أو نص مقتبس مش مطابق.
4. **لو مفيش سند كافي، النظام يقول كده** بدل ما يألّف إجابة.
5. **مفيش أرقام ثقة وهمية.** أي نسبة بتظهر للمحامي لازم تكون محسوبة ومتقاسة على الـ eval set.
6. **التكلفة جزء من التصميم:** أرخص نموذج يكفي، كاش، حدود استخدام، وحد صرف شهري.
7. **خصوصية بيانات العملاء:** كل محامي/مكتب معزول عن الباقي (Row-Level Security)، والالتزام بقانون حماية البيانات الشخصية المصري 151 لسنة 2020.

---

## 2. الـ Tech Stack

| الطبقة | التقنية | ليه |
|---|---|---|
| Backend | **C# / ASP.NET Core Web API (.NET 10 LTS)** | اختيار الفريق، أداء عالي، typing قوي |
| ORM | Entity Framework Core + Npgsql | Postgres أصلي، migrations |
| Database | **PostgreSQL على Supabase (Free tier)** | Full-Text Search مدمج، pgvector لاحقاً، Auth جاهز |
| البحث | Postgres Full-Text Search (BM25-like ranking عبر `ts_rank_cd`) + توحيد النص العربي في الكود | ببلاش، مفيش سيرفر بحث منفصل في الـ pilot |
| Auth | Supabase Auth (JWT) + `JwtBearer` في ASP.NET | ببلاش، تسجيل وإيميل جاهزين |
| Background jobs | Hangfire (تخزين على Postgres) | معالجة المستندات والتحليلات الطويلة |
| LLM | **Anthropic C# SDK** — نموذج Haiku أساسي، Sonnet للتحليل العميق بحد استخدام | أرخص تكلفة مع جودة كافية |
| استخراج نص PDF | UglyToad.PdfPig | مفتوح المصدر وببلاش |
| مطابقة الأسماء | Jaro-Winkler / Levenshtein (تنفيذ داخلي في `Core`) | ببلاش ومتختبر |
| Frontend | **Next.js + TypeScript + Tailwind (RTL)** | مبني على تصميم `Laywer_Dashboard.html` |
| Unit/Integration tests | xUnit + Testcontainers (Postgres) | tests حقيقية على Postgres |
| E2E tests | Playwright | سيناريوهات المحامي من أولها لآخرها |
| CI | GitHub Actions | build + test مع كل push |
| Deploy | Docker → Oracle Cloud Always Free (API) + Cloudflare Pages (Web) | ببلاش |
| Observability | Serilog + جدول `llm_usage` للتكلفة | متابعة الصرف لكل محامي |

---

## 3. هيكل الـ Repository

```
smart-lawyer/
├── PLAN.md                         ← الملف ده
├── README.md                       ← تشغيل المشروع محلياً
├── SmartLawyer.slnx
├── src/
│   ├── SmartLawyer.Api/            ← ASP.NET Core Web API (controllers/endpoints, auth, quotas)
│   ├── SmartLawyer.Core/           ← Domain + الخوارزميات (من غير أي dependency خارجي)
│   │   ├── Arabic/                 ← توحيد النص العربي، استخراج التواريخ والأرقام
│   │   ├── Timeline/               ← Simple Temporal Network + Floyd-Warshall
│   │   ├── Contradictions/         ← مقارنة رقمية/زمنية + مطابقة كيانات
│   │   ├── Deadlines/              ← محرك قواعد المواعيد الإجرائية
│   │   └── Citations/              ← Citation Verifier
│   ├── SmartLawyer.Infrastructure/ ← EF Core, البحث, Anthropic client, التخزين
│   └── SmartLawyer.Ingestion/      ← Console app لاستيراد القوانين والأحكام
├── tests/
│   ├── SmartLawyer.Core.Tests/
│   ├── SmartLawyer.Api.Tests/      ← Integration (Testcontainers)
│   └── e2e/                        ← Playwright
├── eval/                           ← أسئلة التقييم + أداة القياس
├── apps/
│   └── web/                        ← Next.js frontend
├── deploy/                         ← Dockerfiles, docker-compose, إعدادات السيرفر
└── .github/workflows/              ← CI
```

قاعدة: `Core` مايعتمدش على أي حاجة (لا EF ولا HTTP ولا AI). ده بيخلي الخوارزميات سهلة الاختبار.

---

## 4. الـ Milestones

كل milestone بيتسلم بـ: كود على الـ repo + tests ناجحة في CI + ملخص للي اتعمل. **مفيش بدء في milestone جديد قبل موافقة صاحب المشروع.**

### M0 — الإعداد
- [x] Solution و projects حسب الهيكل فوق
- [x] `.gitignore`، `.editorconfig`، `.env.example`، `README.md`
- [x] GitHub Actions: `dotnet build` + `dotnet test` (الـ lint بتاع الـ web هيتضاف مع M1)
- [x] `docker-compose.yml` للتشغيل المحلي (Postgres)
- [x] Endpoint `/health` + أول integration test
- **يخلص لما:** الـ CI أخضر على أول push.

### M1 — الـ Frontend
- [ ] تحويل `Laywer_Dashboard.html` لـ Next.js components، كل قسم route مستقل
- [ ] Login / تسجيل بـ Supabase Auth
- [ ] إصلاح: أخطاء الكتابة («ببدفتر»، «نتائح»)، البيانات المتعارضة بين الأقسام، أرقام الثقة من غير مصدر
- [ ] صفحة الباقات بأسعار بالجنيه
- [ ] Responsive على الموبايل
- **يخلص لما:** محامي يسجل ويتنقل في كل الأقسام (ببيانات تجريبية).

### M2 — الـ Database والبيانات القانونية
- [ ] Schema:
  - `laws`, `articles`, `article_versions (valid_from, valid_to)`
  - `rulings (court, appeal_no, judicial_year, session_date, chamber, principle, text)`
  - `citations (ruling_id → article_id / ruling_id)`
  - `cases`, `documents`, `events`, `claims`
  - `users`, `organizations`, `usage_quota`, `llm_usage`
- [ ] Row-Level Security لبيانات القضايا
- [ ] `ArabicNormalizer`: توحيد الألف، الياء، التاء المربوطة (في الفهرس بس)، حذف التشكيل والتطويل، تحويل الأرقام الهندية
- [ ] Ingestion: استخراج النص من PDF ← تقسيم القانون لمواد ← تقسيم الحكم لحقوله
- [ ] Full-Text Search index + بحث برقم المادة / رقم الطعن
- **البيانات:** القانون المدني، التجارة، المرافعات + أول دفعة أحكام نقض (من مصادر مسموح باستخدامها فقط — بنراجع شروط كل مصدر قبل الاستخدام).
- **يخلص لما:** البحث بكلمة أو برقم مادة يرجّع النتيجة الصح، والـ tests بتغطي الـ normalizer والتقسيم.

### M3 — البحث القانوني والـ Research Agent
- [ ] Tools (كلها من قاعدة البيانات، مش من الإنترنت):
  - `search_legislation(query, as_of_date)`
  - `get_article(law_id, article_no, as_of_date)`
  - `search_cassation(query, chamber, year_range)`
  - `get_ruling(appeal_no, judicial_year)`
  - `compute_deadline(event, date, procedure)`
- [ ] Agent loop بالـ Anthropic C# SDK (tool use، strict schemas)
- [ ] توسيع السؤال بالمرادفات (بديل رخيص للبحث الدلالي)
- [ ] Citation Verifier إجباري قبل عرض الإجابة
- [ ] كاش للإجابات المتكررة + Prompt caching
- [ ] حدود استخدام لكل محامي + تسجيل التكلفة في `llm_usage`
- **يخلص لما:** كل إجابة فيها مصادر حقيقية قابلة للضغط، وصفر استشهادات غير موجودة.

### M4 — تحليل القضايا، الخط الزمني، التناقضات
- [ ] رفع المستندات (PDF) + استخراج النص في background job
- [ ] استخراج منظم (Structured Output): الأطراف، الوقائع، التواريخ، المبالغ، الأدلة
- [ ] **الخط الزمني:** استخراج التواريخ العربية بالـ regex ← Simple Temporal Network ← كشف التعارض (negative cycle بـ Floyd-Warshall) ← عرض تفاعلي
- [ ] **التناقضات الرقمية والزمنية:** مقارنة حسابية بهامش مسموح + مطابقة الكيانات (Jaro-Winkler)
- [ ] **حاسبة المواعيد:** محرك قواعد ثابت + الإجازات الرسمية — **مراجعة القواعد من محامي قبل الإطلاق إجبارية**
- [ ] نقاط القوة والضعف، كل نقطة مربوطة بمصدر
- **يخلص لما:** ملف قضية حقيقي يطلع منه timeline + تعارضات + تحليل بالمصادر.

### M5 — الـ Testing والتقييم
- [ ] Unit tests لكل خوارزم في `Core` (هدف تغطية ≥ 90% لـ `Core`)
- [ ] Integration tests للـ API على Postgres حقيقي (Testcontainers)
- [ ] E2E بـ Playwright: تسجيل ← سؤال بحث ← رفع قضية ← timeline
- [ ] Eval set: 100 سؤال بإجاباتهم الصح من محامين
- [ ] أداة قياس: Recall@k، دقة الاستشهادات، نسبة الهلوسة، التكلفة لكل سؤال
- [ ] ضبط إعدادات البحث (عدد النتائج، أوزان الترتيب) على الـ eval set
- **يخلص لما:** CI أخضر + تقرير دقة حقيقي.

### M6 — إطلاق التجربة (20 محامي)
- [ ] Deploy: API على Oracle Free (Docker)، Web على Cloudflare Pages
- [ ] Dashboard للاستهلاك والتكلفة
- [ ] زرار تقييم (👍/👎 + تعليق) على كل إجابة
- [ ] Backup يومي للـ database
- [ ] رسالة ترحيب وشرح استخدام
- **يخلص لما:** الـ 20 محامي شغالين، والتكلفة والتقييمات بتتسجل.

### M7 — بعد التجربة (حسب النتائج)
- التناقضات في المعنى (NLI)
- محاكي المحاكمة (قاضي + محامي خصم + مقيّم، Elo لتكييف الصعوبة)
- الدفع (Paymob / Fawry / InstaPay)
- تطبيق موبايل
- بحث دلالي بالـ embeddings لو الـ eval أثبت إنه هيفرق

---

## 5. ميزانية الـ Pilot (تقديرية — 20 محامي / شهر)

| البند | دولار | جنيه (×53) |
|---|---|---|
| Haiku (بحث + تحليل) | 12 – 15 | 640 – 800 |
| Sonnet (5 تحليلات عميقة/محامي) | ~16 أو 0 | ~850 |
| الاستضافة | 0 | 0 |
| **الإجمالي** | **12 – 31** | **640 – 1,650** |

ضوابط التكلفة:
- حد صرف شهري في Anthropic Console (مثلاً $25).
- حدود يومية لكل محامي (افتراضي: 15 سؤال، 3 تحليلات قضايا يومياً، 5 تحليلات عميقة شهرياً).
- كل طلب للنموذج يفضل تحت 100 ألف token.
- `effort` منخفض للمهام البسيطة.
- كاش للإجابات + Prompt caching لملفات القضايا.

---

## 6. التعليمات لأي حد (أو أي AI) بيكتب كود في المشروع

### الكود
- C#: `nullable` مفعّل، `TreatWarningsAsErrors` في `Core`، أسماء بالإنجليزي، والتعليقات بالعربي أو الإنجليزي.
- أي منطق قانوني أو حسابي يتحط في `SmartLawyer.Core` ومعاه tests.
- مفيش استدعاء للـ AI في أي مكان غير `Infrastructure/Llm`.
- كل tool للـ agent ليها JSON schema صارم، والـ input بيتعمله validation قبل التنفيذ.
- كل استدعاء للنموذج بيتسجل في `llm_usage` (المستخدم، عدد الـ tokens، التكلفة).

### الأسرار والأمان
- **ممنوع أي API key أو كلمة سر في الـ repo.** القيم في environment variables أو user-secrets، و`.env.example` فيه الأسماء بس.
- بيانات القضايا ماتتبعتش لأي خدمة غير اللازمة للمعالجة.
- أي مصدر بيانات قانونية جديد: نراجع شروط الاستخدام قبل جمعه.

### Git
- branch لكل milestone: `m0-setup`, `m1-frontend`, ...
- Commit messages واضحة (`feat:`, `fix:`, `test:`, `docs:`, `chore:`).
- مفيش merge غير والـ CI أخضر.

### التشغيل المحلي (هيتفصّل في README مع M0)
```bash
docker compose up -d postgres
dotnet run --project src/SmartLawyer.Api
cd apps/web && npm install && npm run dev
```

---

## 7. المطلوب من صاحب المشروع

| إمتى | المطلوب |
|---|---|
| M0 | GitHub repo + Anthropic API key (عن طريق الـ form الآمن، مش في الشات) + حد صرف شهري في الـ Console |
| M1 | مشروع Supabase والـ keys بتاعته (بنفس الطريقة الآمنة) |
| M2 | ملفات القوانين والأحكام المسموح باستخدامها |
| M4 | محامي يراجع قواعد المواعيد الإجرائية والتحليل |
| M5 | 100 سؤال وإجاباتهم الصح من المحامين |
| M6 | حساب Oracle Cloud، ودومين (اختياري) |

> **تنويه:** المنصة أداة مساعدة. المراجعة القانونية النهائية مسؤولية المحامي.
