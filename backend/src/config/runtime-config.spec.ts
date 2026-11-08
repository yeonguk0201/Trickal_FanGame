import { parseCorsOrigins, resolveRuntimeConfig } from './runtime-config';

describe('resolveRuntimeConfig', () => {
  it('uses local defaults outside production', () => {
    expect(resolveRuntimeConfig({ NODE_ENV: 'development' })).toEqual({
      port: 3001,
      corsOrigins: ['http://localhost:3000'],
    });
  });

  it('reads the port and every CORS origin', () => {
    expect(
      resolveRuntimeConfig({
        NODE_ENV: 'production',
        DATABASE_URL: 'postgresql://example',
        PORT: '10000',
        CORS_ORIGIN: 'https://trickal.vercel.app/, http://localhost:3000',
      }),
    ).toEqual({
      port: 10000,
      corsOrigins: ['https://trickal.vercel.app', 'http://localhost:3000'],
    });
  });

  it('fails in production when required variables are missing', () => {
    expect(() => resolveRuntimeConfig({ NODE_ENV: 'production' })).toThrow(
      'DATABASE_URL, CORS_ORIGIN',
    );
    expect(() =>
      resolveRuntimeConfig({
        NODE_ENV: 'production',
        DATABASE_URL: 'postgresql://example',
        CORS_ORIGIN: '  ',
      }),
    ).toThrow('CORS_ORIGIN');
  });

  it('rejects an invalid port', () => {
    expect(() => resolveRuntimeConfig({ PORT: 'abc' })).toThrow('PORT');
  });
});

describe('parseCorsOrigins', () => {
  it('drops empty entries and trailing slashes', () => {
    expect(parseCorsOrigins('https://a.app/,, https://b.app ')).toEqual([
      'https://a.app',
      'https://b.app',
    ]);
  });
});
