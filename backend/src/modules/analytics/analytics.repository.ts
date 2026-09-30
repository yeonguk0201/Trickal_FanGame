import { Injectable } from '@nestjs/common';
import { PrismaService } from '../../database/prisma.service';
import type { AnalyticsCatalog } from './analytics.types';

@Injectable()
export class AnalyticsRepository {
  constructor(private readonly prisma: PrismaService) {}

  async loadCatalog(): Promise<AnalyticsCatalog> {
    return this.prisma.$transaction(async (transaction) => {
      const users = await transaction.user.findMany({
        orderBy: { nickname: 'asc' },
        select: { id: true, nickname: true },
      });
      const characters = await transaction.character.findMany({
        orderBy: { id: 'asc' },
        select: { id: true, name: true },
      });
      const items = await transaction.item.findMany({
        orderBy: { id: 'asc' },
        select: { id: true, name: true },
      });
      const runs = await transaction.run.findMany({
        select: {
          id: true,
          userId: true,
          characterId: true,
          playTime: true,
          reachedFloor: true,
          isCleared: true,
          endedAt: true,
          user: { select: { nickname: true } },
          character: { select: { name: true } },
          runItems: { select: { itemId: true } },
        },
      });

      return {
        users,
        characters,
        items,
        runs: runs.map((run) => ({
          id: run.id,
          userId: run.userId,
          nickname: run.user.nickname,
          characterId: run.characterId,
          characterName: run.character.name,
          playTime: run.playTime,
          reachedFloor: run.reachedFloor,
          isCleared: run.isCleared,
          endedAt: run.endedAt,
          itemIds: [...new Set(run.runItems.map((runItem) => runItem.itemId))],
        })),
      };
    });
  }
}
