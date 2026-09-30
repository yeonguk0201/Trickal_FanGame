import { Type } from 'class-transformer';
import { IsEnum, IsInt, IsOptional, Max, Min } from 'class-validator';

export enum RankingType {
  HIGHEST_FLOOR = 'highest-floor',
  FASTEST_CLEAR = 'fastest-clear',
  MOST_CLEARS = 'most-clears',
}

export class GetRankingsQueryDto {
  @IsOptional()
  @IsEnum(RankingType)
  type?: RankingType;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(1000000)
  page?: number;

  @IsOptional()
  @Type(() => Number)
  @IsInt()
  @Min(1)
  @Max(100)
  limit?: number;
}
