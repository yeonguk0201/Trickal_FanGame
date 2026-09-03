import { HttpStatus, Injectable } from '@nestjs/common';
import {
  CHARACTER_MAX_LEVEL,
  EXPERIENCE_TO_NEXT_LEVEL,
  SKILL_MAX_LEVEL,
  type CharacterProgressSnapshot,
} from '../../contracts/meta-progression';
import { ApiException } from '../../common/exceptions/api.exception';
import { GetUserRunsQueryDto } from './dto/get-user-runs-query.dto';
import {
  UsersRepository,
  UsersRepositoryError,
  type SkillType,
} from './users.repository';

@Injectable()
export class UsersService {
  constructor(private readonly usersRepository: UsersRepository) {}

  async searchUsers(nickname: string) {
    const user = await this.usersRepository.findPublicByNickname(nickname);

    return user ? [user] : [];
  }

  async getUser(nickname: string) {
    const user =
      await this.usersRepository.findByNicknameWithProgress(nickname);

    if (!user) {
      throw this.userNotFound();
    }

    const stats = await this.usersRepository.getRunStats(user.id);

    return {
      id: user.id,
      nickname: user.nickname,
      stats: {
        totalRuns: stats.totalRuns,
        clears: stats.clears,
        winRate:
          stats.totalRuns === 0 ? 0 : (stats.clears / stats.totalRuns) * 100,
        averagePlayTime: stats.averagePlayTime ?? 0,
        averageFloor: stats.averageFloor ?? 0,
        highestFloor: stats.highestFloor ?? 0,
      },
      characterProgress: user.characterProgress.map((progress) => ({
        characterId: progress.characterId,
        characterName: progress.character.name,
        level: progress.level,
        maxLevel: CHARACTER_MAX_LEVEL,
        experience: progress.experience,
        experienceToNextLevel: this.experienceToNextLevel(progress.level),
        skillPoints: progress.skillPoints,
        lowGradeSkillLevel: progress.lowGradeSkillLevel,
        highGradeSkillLevel: progress.highGradeSkillLevel,
        maxSkillLevel: SKILL_MAX_LEVEL,
      })),
    };
  }

  async upgradeSkill(
    nickname: string,
    characterId: string,
    skillTypeValue: string,
    targetLevel: number,
  ): Promise<CharacterProgressSnapshot> {
    const skillType = this.parseSkillType(skillTypeValue);

    try {
      return await this.usersRepository.upgradeSkill({
        nickname,
        characterId,
        skillType,
        targetLevel,
      });
    } catch (error: unknown) {
      if (error instanceof UsersRepositoryError) {
        throw this.toApiException(error);
      }
      throw error;
    }
  }

  async getRunHistory(nickname: string, query: GetUserRunsQueryDto) {
    const user = await this.usersRepository.findByNickname(nickname);

    if (!user) {
      throw this.userNotFound();
    }

    const page = query.page ?? 1;
    const limit = query.limit ?? 20;
    const { runs, total } = await this.usersRepository.findRunsByUserId(
      user.id,
      { page, limit },
    );
    const totalPages = Math.max(1, Math.ceil(total / limit));

    if (page > totalPages) {
      throw new ApiException(
        HttpStatus.UNPROCESSABLE_ENTITY,
        'RUN_PAGE_OUT_OF_RANGE',
        '요청한 전적 페이지가 범위를 벗어났습니다.',
      );
    }

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
      meta: { page, limit, total, totalPages },
    };
  }

  private experienceToNextLevel(level: number): number {
    return level >= CHARACTER_MAX_LEVEL
      ? 0
      : EXPERIENCE_TO_NEXT_LEVEL[level - 1];
  }

  private parseSkillType(value: string): SkillType {
    if (value === 'LOW_GRADE' || value === 'HIGH_GRADE') {
      return value;
    }

    throw new ApiException(
      HttpStatus.UNPROCESSABLE_ENTITY,
      'INVALID_SKILL_TYPE',
      '유효하지 않은 스킬 종류입니다.',
    );
  }

  private toApiException(error: UsersRepositoryError): ApiException {
    switch (error.code) {
      case 'USER_NOT_FOUND':
      case 'CHARACTER_PROGRESS_NOT_FOUND':
        return new ApiException(
          HttpStatus.NOT_FOUND,
          error.code,
          error.message,
        );
      case 'SKILL_POINT_NOT_ENOUGH':
      case 'SKILL_LEVEL_MAX':
      case 'INVALID_SKILL_TARGET_LEVEL':
        return new ApiException(
          HttpStatus.UNPROCESSABLE_ENTITY,
          error.code,
          error.message,
        );
    }
  }

  private userNotFound(): ApiException {
    return new ApiException(
      HttpStatus.NOT_FOUND,
      'USER_NOT_FOUND',
      '존재하지 않는 유저입니다.',
    );
  }
}
