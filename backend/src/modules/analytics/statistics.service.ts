import { HttpStatus, Injectable } from '@nestjs/common';
import { ApiException } from '../../common/exceptions/api.exception';
import { AnalyticsRepository } from './analytics.repository';
import { buildStatistics, buildUserStatistics } from './analytics.domain';

@Injectable()
export class StatisticsService {
  constructor(private readonly repository: AnalyticsRepository) {}

  async getOverview() {
    return (await this.all()).overview;
  }

  async getCharacters() {
    return (await this.all()).characters;
  }

  async getItems() {
    return (await this.all()).items;
  }

  async getFloors() {
    return (await this.all()).floors;
  }

  async getUser(nickname: string) {
    const catalog = await this.repository.loadCatalog();
    const user = catalog.users.find(
      (candidate) => candidate.nickname === nickname,
    );
    if (!user) {
      throw new ApiException(
        HttpStatus.NOT_FOUND,
        'USER_NOT_FOUND',
        '존재하지 않는 유저입니다.',
      );
    }
    return {
      nickname: user.nickname,
      ...buildUserStatistics(catalog, user.id),
    };
  }

  private async all() {
    return buildStatistics(await this.repository.loadCatalog());
  }
}
