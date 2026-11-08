import { HttpStatus } from '@nestjs/common';
import { readClientProfileKey } from './client-profile-key';

describe('readClientProfileKey', () => {
  const key = '30000000-0000-4000-8000-000000000001';

  it('accepts a UUID header and normalizes its case', () => {
    expect(readClientProfileKey(key.toUpperCase())).toBe(key);
    expect(readClientProfileKey([key])).toBe(key);
  });

  it.each([undefined, '', 'not-a-uuid', 123])(
    'rejects a missing or malformed key (%p)',
    (value) => {
      let error: unknown;
      try {
        readClientProfileKey(value);
      } catch (caught: unknown) {
        error = caught;
      }
      expect(error).toMatchObject({
        status: HttpStatus.UNAUTHORIZED,
        response: { error: { code: 'PROFILE_KEY_REQUIRED' } },
      });
    },
  );
});
