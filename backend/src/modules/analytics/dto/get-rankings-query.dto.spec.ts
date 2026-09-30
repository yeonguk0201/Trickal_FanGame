import 'reflect-metadata';
import { plainToInstance } from 'class-transformer';
import { validate } from 'class-validator';
import { GetRankingsQueryDto, RankingType } from './get-rankings-query.dto';

describe('GetRankingsQueryDto', () => {
  it('transforms and accepts a valid query', async () => {
    const dto = plainToInstance(GetRankingsQueryDto, {
      type: RankingType.FASTEST_CLEAR,
      page: '2',
      limit: '50',
    });
    await expect(validate(dto)).resolves.toHaveLength(0);
    expect(dto).toEqual({ type: 'fastest-clear', page: 2, limit: 50 });
  });

  it.each([
    { type: 'unknown' },
    { page: '0' },
    { limit: '101' },
    { limit: '1.5' },
  ])('rejects invalid query values: %p', async (query) => {
    const dto = plainToInstance(GetRankingsQueryDto, query);
    expect(await validate(dto)).not.toHaveLength(0);
  });
});
