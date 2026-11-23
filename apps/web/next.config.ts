import type { NextConfig } from "next";

// STATIC_EXPORT=1 بيطلّع نسخة ثابتة (من غير سيرفر) للعرض على GitHub Pages ببيانات تجريبية.
const isStaticExport = process.env.STATIC_EXPORT === "1";

const nextConfig: NextConfig = isStaticExport
  ? {
      output: "export",
      basePath: process.env.PAGES_BASE_PATH ?? "",
      trailingSlash: true,
      images: { unoptimized: true },
    }
  : {};

export default nextConfig;
