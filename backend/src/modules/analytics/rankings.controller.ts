import { Controller, Get, Query } from '@nestjs/common';
import { GetRankingsQueryDto } from './dto/get-rankings-query.dto';
import { RankingsService } from './rankings.service';

@Controller('rankings')
export class RankingsController {
  constructor(private readonly service: RankingsService) {}

  @Get()
  async getRankings(@Query() query: GetRankingsQueryDto) {
    const { data, meta } = await this.service.getRankings(query);
    return { success: true, data, meta };
  }
}
