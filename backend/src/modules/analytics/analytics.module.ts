import { Module } from '@nestjs/common';
import { AnalyticsRepository } from './analytics.repository';
import { RankingsController } from './rankings.controller';
import { RankingsService } from './rankings.service';
import { StatisticsController } from './statistics.controller';
import { StatisticsService } from './statistics.service';

@Module({
  controllers: [StatisticsController, RankingsController],
  providers: [AnalyticsRepository, StatisticsService, RankingsService],
})
export class AnalyticsModule {}
