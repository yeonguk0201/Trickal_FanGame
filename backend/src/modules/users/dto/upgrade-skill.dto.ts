import { Type } from 'class-transformer';
import { IsInt } from 'class-validator';

export class UpgradeSkillDto {
  @Type(() => Number)
  @IsInt()
  targetLevel!: number;
}
