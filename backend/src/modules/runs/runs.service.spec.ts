import { HttpStatus } from '@nestjs/common';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import { ApiException } from '../../common/exceptions/api.exception';
import { CreateRunDto, DeathReason } from './dto/create-run.dto';
import {
  RunsRepository,
  RunsRepositoryError,
  type PersistRunResult,
} from './runs.repository';
import { RunsService } from './runs.service';

describe('RunsService', () => {
  let service: RunsService;
  let repository: { create: jest.Mock; findDetailById: jest.Mock };

  const validDto: CreateRunDto = {
    clientRunId: '10000000-0000-4000-8000-000000000001',
    userId: '00000000-0000-4000-8000-000000000001',
    characterId: 'erpin',
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
  const persistedResult: PersistRunResult = {
    runId: '20000000-0000-4000-8000-000000000001',
    experienceGained: 840,
    progress: {
      characterId: 'erpin',
      level: 2,
      experience: 440,
      experienceToNextLevel: 500,
      skillPoints: 1,
      lowGradeSkillLevel: 1,
      highGradeSkillLevel: 1,
    },
    created: true,
  };

  beforeEach(() => {
    repository = {
      create: jest.fn().mockResolvedValue(persistedResult),
      findDetailById: jest.fn().mockResolvedValue({
        id: 'run-id',
        user: { id: 'user-id', nickname: 'test-player' },
        character: { id: 'erpin', name: '에르핀' },
        gameVersion: '0.1.0',
        startedAt: new Date('2026-08-17T10:00:00.000Z'),
        endedAt: new Date('2026-08-17T10:10:00.000Z'),
        playTime: 600,
        reachedFloor: 3,
        isCleared: true,
        killCount: 100,
        deathReason: null,
        runItems: [
          {
            itemId: 'item-01',
            floor: 1,
            itemOrder: 1,
            acquiredAt: new Date('2026-08-17T10:02:00.000Z'),
            item: { name: '우주를 담은 보석', rarity: 'COMMON' },
          },
        ],
      }),
    };
    service = new RunsService(repository as unknown as RunsRepository);
  });

  it('stores a valid cleared run through the atomic repository operation', async () => {
    await expect(service.create(validDto)).resolves.toEqual(persistedResult);
    expect(repository.create).toHaveBeenCalledWith(validDto);
  });

  it('returns a web-ready Run detail with acquisition order', async () => {
    await expect(service.getDetail('run-id')).resolves.toMatchObject({
      id: 'run-id',
      user: { nickname: 'test-player' },
      character: { id: 'erpin', name: '에르핀' },
      items: [
        {
          itemId: 'item-01',
          name: '우주를 담은 보석',
          rarity: 'COMMON',
          floor: 1,
          order: 1,
        },
      ],
    });
  });

  it('rejects an unknown Run detail', async () => {
    repository.findDetailById.mockResolvedValue(null);

    await expectApiError(
      service.getDetail('missing'),
      HttpStatus.NOT_FOUND,
      'RUN_NOT_FOUND',
    );
  });

  it('accepts a valid death run', async () => {
    const deathDto = {
      ...validDto,
      isCleared: false,
      deathReason: DeathReason.BOSS,
    };

    await expect(service.create(deathDto)).resolves.toEqual(persistedResult);
    expect(repository.create).toHaveBeenCalledWith(deathDto);
  });

  it('rejects inconsistent clear and death data before opening a transaction', async () => {
    const dto = {
      ...validDto,
      deathReason: DeathReason.BOSS,
    };

    await expectApiError(
      service.create(dto),
      HttpStatus.UNPROCESSABLE_ENTITY,
      'INVALID_RUN_DATA',
    );
    expect(repository.create).not.toHaveBeenCalled();
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
    expect(repository.create).not.toHaveBeenCalled();
  });

  it.each([
    ['USER_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['CHARACTER_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['ITEM_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['RUN_IDEMPOTENCY_CONFLICT', HttpStatus.CONFLICT],
    ['RUN_RESULT_UNAVAILABLE', HttpStatus.INTERNAL_SERVER_ERROR],
  ] as const)('maps repository error %s to HTTP %s', async (code, status) => {
    repository.create.mockRejectedValue(new RunsRepositoryError(code, code));

    await expectApiError(service.create(validDto), status, code);
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
