import { HttpStatus, Injectable } from '@nestjs/common';
import { ApiException } from '../../common/exceptions/api.exception';
import type { CreateRunDto } from './dto/create-run.dto';
import { RunsRepository, RunsRepositoryError } from './runs.repository';

@Injectable()
export class RunsService {
  constructor(private readonly runsRepository: RunsRepository) {}

  async getDetail(runId: string) {
    const run = await this.runsRepository.findDetailById(runId);
    if (!run) {
      throw new ApiException(
        HttpStatus.NOT_FOUND,
        'RUN_NOT_FOUND',
        '존재하지 않는 Run입니다.',
      );
    }

    return {
      id: run.id,
      user: run.user,
      character: run.character,
      gameVersion: run.gameVersion,
      startedAt: run.startedAt,
      endedAt: run.endedAt,
      playTime: run.playTime,
      reachedFloor: run.reachedFloor,
      isCleared: run.isCleared,
      killCount: run.killCount,
      deathReason: run.deathReason,
      items: run.runItems.map((runItem) => ({
        itemId: runItem.itemId,
        name: runItem.item.name,
        rarity: runItem.item.rarity,
        floor: runItem.floor,
        order: runItem.itemOrder,
        acquiredAt: runItem.acquiredAt,
      })),
    };
  }

  async create(dto: CreateRunDto) {
    this.validateRunData(dto);

    try {
      return await this.runsRepository.create(dto);
    } catch (error: unknown) {
      if (error instanceof RunsRepositoryError) {
        throw this.toApiException(error);
      }
      throw error;
    }
  }

  private toApiException(error: RunsRepositoryError): ApiException {
    switch (error.code) {
      case 'USER_NOT_FOUND':
      case 'CHARACTER_NOT_FOUND':
      case 'ITEM_NOT_FOUND':
        return new ApiException(
          HttpStatus.NOT_FOUND,
          error.code,
          error.message,
        );
      case 'RUN_IDEMPOTENCY_CONFLICT':
        return new ApiException(HttpStatus.CONFLICT, error.code, error.message);
      case 'RUN_RESULT_UNAVAILABLE':
        return new ApiException(
          HttpStatus.INTERNAL_SERVER_ERROR,
          error.code,
          error.message,
        );
    }
  }

  private validateRunData(dto: CreateRunDto) {
    const startedAt = new Date(dto.startedAt);
    const endedAt = new Date(dto.endedAt);
    const hasDeathReason =
      dto.deathReason !== null && dto.deathReason !== undefined;
    const sortedOrders = dto.items
      .map((item) => item.order)
      .sort((a, b) => a - b);
    const hasSequentialOrders = sortedOrders.every(
      (order, index) => order === index + 1,
    );
    const hasInvalidItemFloor = dto.items.some(
      (item) => item.floor > dto.reachedFloor,
    );
    const hasInvalidAcquisitionTime = dto.items.some((item) => {
      const acquiredAt = new Date(item.acquiredAt);
      return acquiredAt < startedAt || acquiredAt > endedAt;
    });

    if (
      endedAt <= startedAt ||
      (dto.isCleared && hasDeathReason) ||
      (!dto.isCleared && !hasDeathReason) ||
      !hasSequentialOrders ||
      hasInvalidItemFloor ||
      hasInvalidAcquisitionTime
    ) {
      throw new ApiException(
        HttpStatus.UNPROCESSABLE_ENTITY,
        'INVALID_RUN_DATA',
        '유효하지 않은 플레이 데이터입니다.',
      );
    }
  }
}
