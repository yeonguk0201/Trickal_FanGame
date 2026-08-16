import { Injectable } from '@nestjs/common';
import { PrismaService } from '../../database/prisma.service';
import type { CreateRunDto } from './dto/create-run.dto';

@Injectable()
export class RunsRepository {
  constructor(private readonly prisma: PrismaService) {}

  findUser(userId: string) {
    return this.prisma.user.findUnique({
      where: { id: userId },
      select: { id: true },
    });
  }

  findActiveCharacter(characterId: string) {
    return this.prisma.character.findFirst({
      where: { id: characterId, isActive: true },
      select: { id: true },
    });
  }

  findActiveItemIds(itemIds: string[]) {
    return this.prisma.item.findMany({
      where: { id: { in: itemIds }, isActive: true },
      select: { id: true },
    });
  }

  async create(dto: CreateRunDto) {
    return this.prisma.$transaction(async (transaction) => {
      const run = await transaction.run.create({
        data: {
          userId: dto.userId,
          characterId: dto.characterId,
          startedAt: new Date(dto.startedAt),
          endedAt: new Date(dto.endedAt),
          playTime: dto.playTime,
          reachedFloor: dto.reachedFloor,
          isCleared: dto.isCleared,
          killCount: dto.killCount,
          deathReason: dto.deathReason ?? null,
        },
        select: { id: true },
      });

      if (dto.items.length > 0) {
        await transaction.runItem.createMany({
          data: dto.items.map((item) => ({
            runId: run.id,
            itemId: item.itemId,
            floor: item.floor,
            itemOrder: item.order,
          })),
        });
      }

      return run.id;
    });
  }
}
