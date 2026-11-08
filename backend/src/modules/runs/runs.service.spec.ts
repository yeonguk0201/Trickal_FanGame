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
  let repository: {
    create: jest.Mock;
    findDetailById: jest.Mock;
    findUserIdByClientProfileId: jest.Mock;
  };
  const profileKey = '30000000-0000-4000-8000-000000000001';
  const ownerUserId = '00000000-0000-4000-8000-000000000001';

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
    meta: { elifBalance: 0, elifGained: 0, elifSpent: 0, shopUpgrades: [] },
    created: true,
  };

  beforeEach(() => {
    repository = {
      create: jest.fn().mockResolvedValue(persistedResult),
      findUserIdByClientProfileId: jest.fn().mockResolvedValue(ownerUserId),
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
        elifGained: 2,
        elifSpent: 1,
        runItems: [
          {
            itemId: 'item-01',
            floor: 1,
            itemOrder: 1,
            acquiredAt: new Date('2026-08-17T10:02:00.000Z'),
            item: { name: '우주를 담은 보석', rarity: 'COMMON' },
          },
          {
            itemId: 'item-01',
            floor: 2,
            itemOrder: 2,
            acquiredAt: new Date('2026-08-17T10:06:00.000Z'),
            item: { name: '우주를 담은 보석', rarity: 'COMMON' },
          },
        ],
      }),
    };
    service = new RunsService(repository as unknown as RunsRepository);
  });

  it('stores a valid cleared run through the atomic repository operation', async () => {
    await expect(service.create(validDto, profileKey)).resolves.toEqual(
      persistedResult,
    );
    expect(repository.create).toHaveBeenCalledWith(validDto);
  });

  it('returns a web-ready Run detail with acquisition order', async () => {
    await expect(service.getDetail('run-id')).resolves.toMatchObject({
      id: 'run-id',
      user: { nickname: 'test-player' },
      character: { id: 'erpin', name: '에르핀' },
      elif: { gained: 2, spent: 1 },
      items: [
        {
          itemId: 'item-01',
          name: '우주를 담은 보석',
          rarity: 'COMMON',
          floor: 1,
          order: 1,
        },
        {
          itemId: 'item-01',
          name: '우주를 담은 보석',
          rarity: 'COMMON',
          floor: 2,
          order: 2,
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

    await expect(service.create(deathDto, profileKey)).resolves.toEqual(
      persistedResult,
    );
    expect(repository.create).toHaveBeenCalledWith(deathDto);
  });

  it('rejects inconsistent clear and death data before opening a transaction', async () => {
    const dto = {
      ...validDto,
      deathReason: DeathReason.BOSS,
    };

    await expectApiError(
      service.create(dto, profileKey),
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
      service.create(dto, profileKey),
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
      service.create(dto, profileKey),
      HttpStatus.UNPROCESSABLE_ENTITY,
      'INVALID_RUN_DATA',
    );
    expect(repository.create).not.toHaveBeenCalled();
  });

  it('accepts a valid Elif result and passes it to the repository', async () => {
    const dto = {
      ...validDto,
      elif: {
        gained: 3,
        spent: 6,
        upgradePurchases: [
          {
            upgradeId: 'shop-upgrade-discount',
            level: 1,
            floor: 1,
            order: 1,
          },
          {
            upgradeId: 'shop-upgrade-discount',
            level: 2,
            floor: 2,
            order: 2,
          },
        ],
      },
    };

    await expect(service.create(dto, profileKey)).resolves.toEqual(
      persistedResult,
    );
    expect(repository.create).toHaveBeenCalledWith(dto);
  });

  it.each([
    [
      'an unknown upgrade ID',
      { upgradeId: 'shop-upgrade-missing', level: 1, floor: 1, order: 1 },
      5,
    ],
    [
      'a level above the catalog maximum',
      { upgradeId: 'shop-upgrade-discount', level: 4, floor: 1, order: 1 },
      5,
    ],
    [
      'a purchase floor above the reached floor',
      { upgradeId: 'shop-upgrade-discount', level: 1, floor: 4, order: 1 },
      5,
    ],
    [
      'spent below the upgrade cost',
      { upgradeId: 'shop-upgrade-discount', level: 1, floor: 1, order: 1 },
      1,
    ],
  ])(
    'rejects %s before opening a transaction',
    async (_label, purchase, spent) => {
      const dto = {
        ...validDto,
        elif: { gained: 0, spent, upgradePurchases: [purchase] },
      };

      await expectApiError(
        service.create(dto, profileKey),
        HttpStatus.UNPROCESSABLE_ENTITY,
        'INVALID_ELIF_DATA',
      );
      expect(repository.create).not.toHaveBeenCalled();
    },
  );

  it('rejects a level skipped within the same Run', async () => {
    const dto = {
      ...validDto,
      elif: {
        gained: 0,
        spent: 10,
        upgradePurchases: [
          { upgradeId: 'shop-upgrade-discount', level: 1, floor: 1, order: 1 },
          { upgradeId: 'shop-upgrade-discount', level: 3, floor: 2, order: 2 },
        ],
      },
    };

    await expectApiError(
      service.create(dto, profileKey),
      HttpStatus.UNPROCESSABLE_ENTITY,
      'INVALID_ELIF_DATA',
    );
  });

  it.each([
    ['USER_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['ELIF_UPGRADE_LEVEL_GAP', HttpStatus.UNPROCESSABLE_ENTITY],
    ['CHARACTER_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['ITEM_NOT_FOUND', HttpStatus.NOT_FOUND],
    ['RUN_IDEMPOTENCY_CONFLICT', HttpStatus.CONFLICT],
    ['RUN_RESULT_UNAVAILABLE', HttpStatus.INTERNAL_SERVER_ERROR],
  ] as const)('maps repository error %s to HTTP %s', async (code, status) => {
    repository.create.mockRejectedValue(new RunsRepositoryError(code, code));

    await expectApiError(service.create(validDto, profileKey), status, code);
  });
});

describe('RunsService profile key', () => {
  const profileKey = '30000000-0000-4000-8000-000000000001';
  const ownerUserId = '00000000-0000-4000-8000-000000000001';
  const dto = {
    clientRunId: '10000000-0000-4000-8000-000000000001',
    characterId: 'erpin',
    gameVersion: '0.1.0',
    startedAt: '2026-08-17T10:00:00.000Z',
    endedAt: '2026-08-17T10:10:00.000Z',
    playTime: 600,
    reachedFloor: 1,
    isCleared: false,
    killCount: 0,
    deathReason: DeathReason.ENEMY,
    items: [],
  } as CreateRunDto;

  function createService(ownerId: string | null) {
    const repository = {
      create: jest.fn().mockResolvedValue({ created: true }),
      findUserIdByClientProfileId: jest.fn().mockResolvedValue(ownerId),
    };
    return {
      repository,
      service: new RunsService(repository as unknown as RunsRepository),
    };
  }

  it('saves the Run for the key owner when userId is omitted', async () => {
    const { repository, service } = createService(ownerUserId);

    await service.create(dto, profileKey);

    expect(repository.findUserIdByClientProfileId).toHaveBeenCalledWith(
      profileKey,
    );
    expect(repository.create).toHaveBeenCalledWith({
      ...dto,
      userId: ownerUserId,
    });
  });

  it('accepts an older request whose userId matches the key owner', async () => {
    const { repository, service } = createService(ownerUserId);

    await service.create({ ...dto, userId: ownerUserId }, profileKey);

    expect(repository.create).toHaveBeenCalledWith({
      ...dto,
      userId: ownerUserId,
    });
  });

  it('rejects a userId that belongs to someone else', async () => {
    const { repository, service } = createService(ownerUserId);

    await expectApiError(
      service.create(
        { ...dto, userId: '00000000-0000-4000-8000-000000000002' },
        profileKey,
      ),
      HttpStatus.FORBIDDEN,
      'PROFILE_KEY_MISMATCH',
    );
    expect(repository.create).not.toHaveBeenCalled();
  });

  it('rejects a key that has no registered user', async () => {
    const { repository, service } = createService(null);

    await expectApiError(
      service.create(dto, profileKey),
      HttpStatus.NOT_FOUND,
      'USER_NOT_FOUND',
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
