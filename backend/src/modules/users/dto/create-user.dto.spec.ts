import { plainToInstance } from 'class-transformer';
import { validate } from 'class-validator';
import { CreateUserDto } from './create-user.dto';

describe('CreateUserDto', () => {
  const clientProfileId = '10000000-0000-4000-8000-000000000001';

  it.each(['Erpin', 'erpin', '에르핀12'])(
    'accepts the case-preserving nickname %s',
    async (nickname) => {
      const dto = plainToInstance(CreateUserDto, {
        clientProfileId,
        nickname: `  ${nickname}  `,
      });

      await expect(validate(dto)).resolves.toHaveLength(0);
      expect(dto.nickname).toBe(nickname);
    },
  );

  it.each([
    [
      'invalid profile id',
      { clientProfileId: 'not-a-uuid', nickname: 'Erpin' },
    ],
    ['too short', { clientProfileId, nickname: 'a' }],
    ['too long', { clientProfileId, nickname: 'a'.repeat(13) }],
    ['symbol', { clientProfileId, nickname: 'Erpin!' }],
    ['non-string', { clientProfileId, nickname: 123 }],
  ])('rejects %s', async (_label, input) => {
    const dto = plainToInstance(CreateUserDto, input);

    await expect(validate(dto)).resolves.not.toHaveLength(0);
  });
});
