import { HttpStatus, Injectable } from '@nestjs/common';
import { ApiException } from '../../common/exceptions/api.exception';
import { GetUserRunsQueryDto } from './dto/get-user-runs-query.dto';
import { UsersRepository } from './users.repository';

@Injectable()
export class UsersService {
  constructor(private readonly usersRepository: UsersRepository) {}

  async getRunHistory(nickname: string, query: GetUserRunsQueryDto) {
    const user = await this.usersRepository.findByNickname(nickname);

    if (!user) {
      throw new ApiException(
        HttpStatus.NOT_FOUND,
        'USER_NOT_FOUND',
        '존재하지 않는 유저입니다.',
      );
    }

    const page = query.page ?? 1;
    const limit = query.limit ?? 20;
    const { runs, total } = await this.usersRepository.findRunsByUserId(
      user.id,
      { page, limit },
    );

    return {
      data: runs.map((run) => ({
        runId: run.id,
        character: run.character,
        reachedFloor: run.reachedFloor,
        playTime: run.playTime,
        isCleared: run.isCleared,
        killCount: run.killCount,
        endedAt: run.endedAt,
      })),
      meta: { page, limit, total },
    };
  }
}
