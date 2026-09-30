import 'reflect-metadata';
import { plainToInstance } from 'class-transformer';
import { validate } from 'class-validator';
import { GetUserRunsQueryDto } from './get-user-runs-query.dto';

describe('GetUserRunsQueryDto', () => {
  it('transforms valid page and limit query strings', async () => {
    const dto = plainToInstance(GetUserRunsQueryDto, {
      page: '2',
      limit: '10',
    });

    await expect(validate(dto)).resolves.toHaveLength(0);
    expect(dto).toMatchObject({ page: 2, limit: 10 });
  });

  it.each([
    ['zero page', { page: '0' }],
    ['excessive page', { page: '1000001' }],
    ['zero limit', { limit: '0' }],
    ['excessive limit', { limit: '101' }],
    ['decimal limit', { limit: '1.5' }],
  ])('rejects %s', async (_label, input) => {
    const dto = plainToInstance(GetUserRunsQueryDto, input);

    await expect(validate(dto)).resolves.not.toHaveLength(0);
  });
});
