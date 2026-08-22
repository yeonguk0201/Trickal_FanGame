import { HttpStatus } from '@nestjs/common';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { ApiException } from '../../common/exceptions/api.exception';
import { CreateRunDto, DeathReason } from './dto/create-run.dto';
import { RunsRepository } from './runs.repository';
import { RunsService } from './runs.service';

describe('RunsService', () => {
  let service: RunsService;
  let repository: {
    findUser: jest.Mock;
    findActiveCharacter: jest.Mock;
    findActiveItemIds: jest.Mock;
    create: jest.Mock;
  };

  const validDto: CreateRunDto = {
    userId: '00000000-0000-4000-8000-000000000001',
    characterId: 'character-a',
    gameVersion: '0.1.0',
    startedAt: '2026-08-17T10:00:00.000Z',
    endedAt: '2026-08-17T10:10:00.000Z',
    playTime: 600,
    reachedFloor: 3,
    isCleared: true,
    killCount: 100,
    deathReason: null,
    items: [
      {
        itemId: 'item-01',
        floor: 1,
        order: 1,
        acquiredAt: '2026-08-17T10:02:00.000Z',
      },
      {
        itemId: 'item-02',
        floor: 2,
        order: 2,
        acquiredAt: '2026-08-17T10:06:00.000Z',
      },
    ],
  };

  beforeEach(() => {
    repository = {
      findUser: jest.fn().mockResolvedValue({ id: validDto.userId }),
      findActiveCharacter: jest
        .fn()
        .mockResolvedValue({ id: validDto.characterId }),
      findActiveItemIds: jest
        .fn()
        .mockResolvedValue([{ id: 'item-01' }, { id: 'item-02' }]),
      create: jest.fn().mockResolvedValue('run-id'),
    };
    service = new RunsService(repository as unknown as RunsRepository);
  });

  it('stores a valid run', async () => {
    await expect(service.create(validDto)).resolves.toBe('run-id');
    expect(repository.create).toHaveBeenCalledWith(validDto);
  });

  it('rejects inconsistent clear and death data', async () => {
    const dto = {
      ...validDto,
      deathReason: DeathReason.BOSS,
    };

    await expectApiError(
      service.create(dto),
      HttpStatus.UNPROCESSABLE_ENTITY,
      'INVALID_RUN_DATA',
    );
    expect(repository.findUser).not.toHaveBeenCalled();
  });

  it('rejects non-sequential item order', async () => {
    const dto = {
      ...validDto,
      items: [
        { ...validDto.items[0], order: 1 },
        { ...validDto.items[1], order: 3 },
      ],
    };

    await expectApiError(
      service.create(dto),
      HttpStatus.UNPROCESSABLE_ENTITY,
      'INVALID_RUN_DATA',
    );
  });

  it('rejects an item acquired outside the run interval', async () => {
    const dto = {
      ...validDto,
      items: [
        {
          ...validDto.items[0],
          acquiredAt: '2026-08-17T10:11:00.000Z',
        },
      ],
    };

    await expectApiError(
      service.create(dto),
      HttpStatus.UNPROCESSABLE_ENTITY,
      'INVALID_RUN_DATA',
    );
    expect(repository.findUser).not.toHaveBeenCalled();
  });

  it('rejects an unknown item', async () => {
    repository.findActiveItemIds.mockResolvedValue([{ id: 'item-01' }]);

    await expectApiError(
      service.create(validDto),
      HttpStatus.NOT_FOUND,
      'ITEM_NOT_FOUND',
    );
    expect(repository.create).not.toHaveBeenCalled();
  });
});

async function expectApiError(
  promise: Promise<unknown>,
  status: HttpStatus,
  code: string,
) {
  try {
    await promise;
    throw new Error('Expected promise to reject');
  } catch (error: unknown) {
    expect(error).toBeInstanceOf(ApiException);

    const apiException = error as ApiException;
    expect(apiException.getStatus()).toBe(status);
    expect(apiException.getResponse()).toMatchObject({
      success: false,
      error: { code },
    });
  }
}
