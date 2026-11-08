export interface RuntimeConfig {
  port: number;
  corsOrigins: string[];
}

const DEFAULT_PORT = 3001;
const DEFAULT_CORS_ORIGIN = 'http://localhost:3000';

// Reads the process environment once at startup. In production a missing
// value fails fast instead of silently falling back to localhost defaults.
export function resolveRuntimeConfig(env: NodeJS.ProcessEnv): RuntimeConfig {
  const isProduction = env.NODE_ENV === 'production';

  if (isProduction) {
    const missing = ['DATABASE_URL', 'CORS_ORIGIN'].filter(
      (key) => !env[key]?.trim(),
    );
    if (missing.length > 0) {
      throw new Error(`운영 환경 변수가 없습니다: ${missing.join(', ')}`);
    }
  }

  const corsOrigins = parseCorsOrigins(env.CORS_ORIGIN ?? DEFAULT_CORS_ORIGIN);
  if (corsOrigins.length === 0) {
    throw new Error('CORS_ORIGIN에 허용할 출처가 없습니다.');
  }

  return {
    port: parsePort(env.PORT),
    corsOrigins,
  };
}

export function parseCorsOrigins(value: string): string[] {
  return value
    .split(',')
    .map((origin) => origin.trim().replace(/\/+$/, ''))
    .filter((origin) => origin.length > 0);
}

function parsePort(value: string | undefined): number {
  if (!value?.trim()) {
    return DEFAULT_PORT;
  }
  const port = Number(value);
  if (!Number.isInteger(port) || port <= 0 || port > 65535) {
    throw new Error(`PORT 값이 올바르지 않습니다: ${value}`);
  }
  return port;
}
