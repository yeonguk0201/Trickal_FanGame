import type { NextConfig } from "next";

// NEXT_PUBLIC_* values are baked in at build time. A Vercel production build without the API URL
// would silently call localhost from every visitor's browser, so stop the build instead.
if (process.env.VERCEL_ENV === "production" && !process.env.NEXT_PUBLIC_API_URL?.trim()) {
  throw new Error("NEXT_PUBLIC_API_URL is required for the production build.");
}

const nextConfig: NextConfig = {};

export default nextConfig;
