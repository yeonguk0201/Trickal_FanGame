import { HttpStatus, Injectable } from '@nestjs/common';
import { ApiException } from '../../common/exceptions/api.exception';
import type { CreateRunDto } from './dto/create-run.dto';
import { RunsRepository } from './runs.repository';

@Injectable()
export class RunsService {
  constructor(private readonly runsRepository: RunsRepository) {}

  async create(dto: CreateRunDto) {
    this.validateRunData(dto);

    const [user, character] = await Promise.all([
      this.runsRepository.findUser(dto.userId),
      this.runsRepository.findActiveCharacter(dto.characterId),
    ]);

    if (!user) {
      throw new ApiException(
        HttpStatus.NOT_FOUND,
        'USER_NOT_FOUND',
        '존재하지 않는 유저입니다.',
      );
    }

    if (!character) {
      throw new ApiException(
        HttpStatus.NOT_FOUND,
        'CHARACTER_NOT_FOUND',
        '존재하지 않는 캐릭터입니다.',
      );
    }

    const requestedItemIds = [...new Set(dto.items.map((item) => item.itemId))];
    const activeItems =
      await this.runsRepository.findActiveItemIds(requestedItemIds);

    if (activeItems.length !== requestedItemIds.length) {
      throw new ApiException(
        HttpStatus.NOT_FOUND,
        'ITEM_NOT_FOUND',
        '존재하지 않는 아이템입니다.',
      );
    }

    return this.runsRepository.create(dto);
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

    if (
      endedAt <= startedAt ||
      (dto.isCleared && hasDeathReason) ||
      (!dto.isCleared && !hasDeathReason) ||
      !hasSequentialOrders ||
      hasInvalidItemFloor
    ) {
      throw new ApiException(
        HttpStatus.UNPROCESSABLE_ENTITY,
        'INVALID_RUN_DATA',
        '유효하지 않은 플레이 데이터입니다.',
      );
    }
  }
}
