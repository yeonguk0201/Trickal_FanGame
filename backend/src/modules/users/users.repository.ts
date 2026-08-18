import { Injectable } from '@nestjs/common';
import { PrismaService } from '../../database/prisma.service';

type FindUserRunsOptions = {
  page: number;
  limit: number;
};

@Injectable()
export class UsersRepository {
  constructor(private readonly prisma: PrismaService) {}

  findByNickname(nickname: string) {
    return this.prisma.user.findUnique({
      where: { nickname },
      select: { id: true },
    });
  }

  async findRunsByUserId(userId: string, options: FindUserRunsOptions) {
    const { page, limit } = options;
    const where = { userId };
    const [runs, total] = await this.prisma.$transaction([
      this.prisma.run.findMany({
        where,
        orderBy: { endedAt: 'desc' },
        skip: (page - 1) * limit,
        take: limit,
        select: {
          id: true,
          reachedFloor: true,
          playTime: true,
          isCleared: true,
          killCount: true,
          endedAt: true,
          character: {
            select: { id: true, name: true },
          },
        },
      }),
      this.prisma.run.count({ where }),
    ]);

    return { runs, total };
  }
}
