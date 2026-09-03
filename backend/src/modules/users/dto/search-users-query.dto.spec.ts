import { plainToInstance } from 'class-transformer';
import { validate } from 'class-validator';
import { SearchUsersQueryDto } from './search-users-query.dto';

describe('SearchUsersQueryDto', () => {
  it('trims a valid exact nickname', async () => {
    const dto = plainToInstance(SearchUsersQueryDto, { q: '  test-player  ' });

    await expect(validate(dto)).resolves.toHaveLength(0);
    expect(dto.q).toBe('test-player');
  });

  it.each([
    ['missing', {}],
    ['empty', { q: '   ' }],
    ['too short', { q: 'a' }],
    ['too long', { q: 'a'.repeat(51) }],
    ['non-string', { q: 123 }],
  ])('rejects %s search input', async (_label, input) => {
    const dto = plainToInstance(SearchUsersQueryDto, input);

    await expect(validate(dto)).resolves.not.toHaveLength(0);
  });
});
