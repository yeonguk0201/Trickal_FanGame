import { Injectable } from '@nestjs/common';
import { AnalyticsRepository } from './analytics.repository';
import { buildRanking } from './analytics.domain';
import { GetRankingsQueryDto, RankingType } from './dto/get-rankings-query.dto';

@Injectable()
export class RankingsService {
  constructor(private readonly repository: AnalyticsRepository) {}

  async getRankings(query: GetRankingsQueryDto) {
    const type = query.type ?? RankingType.HIGHEST_FLOOR;
    const page = query.page ?? 1;
    const limit = query.limit ?? 20;
    const ranking = buildRanking(
      (await this.repository.loadCatalog()).runs,
      type,
    );
    const total = ranking.length;
    const totalPages = Math.max(1, Math.ceil(total / limit));
    const offset = (page - 1) * limit;
    const data = ranking.slice(offset, offset + limit).map((entry, index) => ({
      rank: offset + index + 1,
      ...entry,
    }));
    return { data, meta: { type, page, limit, total, totalPages } };
  }
}
