import { Transform, Type } from 'class-transformer';
import {
  ArrayMaxSize,
  IsArray,
  IsBoolean,
  IsEnum,
  IsInt,
  IsISO8601,
  IsOptional,
  IsString,
  IsUUID,
  Max,
  MaxLength,
  Min,
  ValidateNested,
} from 'class-validator';
import {
  ELIF_RUN_GAIN_MAX,
  ELIF_UPGRADE_PURCHASES_MAX,
} from '../../../contracts/permanent-elif';

export enum DeathReason {
  ENEMY = 'ENEMY',
  BOSS = 'BOSS',
  HAZARD = 'HAZARD',
  UNKNOWN = 'UNKNOWN',
}

export class CreateRunItemDto {
  @IsString()
  @MaxLength(64)
  itemId: string;

  @IsInt()
  @Min(1)
  floor: number;

  @IsInt()
  @Min(1)
  order: number;

  @IsISO8601({ strict: true })
  acquiredAt: string;
}

export class CreateRunElifUpgradePurchaseDto {
  @IsString()
  @MaxLength(64)
  upgradeId: string;

  @IsInt()
  @Min(1)
  level: number;

  @IsInt()
  @Min(1)
  floor: number;

  @IsInt()
  @Min(1)
  order: number;
}

export class CreateRunElifDto {
  @IsInt()
  @Min(0)
  @Max(ELIF_RUN_GAIN_MAX)
  gained: number;

  @IsInt()
  @Min(0)
  spent: number;

  @IsArray()
  @ArrayMaxSize(ELIF_UPGRADE_PURCHASES_MAX)
  @ValidateNested({ each: true })
  @Type(() => CreateRunElifUpgradePurchaseDto)
  upgradePurchases: CreateRunElifUpgradePurchaseDto[];
}

export class CreateRunDto {
  @IsUUID()
  clientRunId: string;

  @IsUUID()
  userId: string;

  @IsString()
  @MaxLength(64)
  characterId: string;

  @IsString()
  @MaxLength(32)
  gameVersion: string;

  @IsISO8601({ strict: true })
  startedAt: string;

  @IsISO8601({ strict: true })
  endedAt: string;

  @IsInt()
  @Min(0)
  playTime: number;

  @IsInt()
  @Min(1)
  reachedFloor: number;

  @IsBoolean()
  isCleared: boolean;

  @IsInt()
  @Min(0)
  killCount: number;

  @IsOptional()
  @Transform(({ value }) => (value === '' ? null : value))
  @IsEnum(DeathReason)
  deathReason?: DeathReason | null;

  @IsArray()
  @ArrayMaxSize(100)
  @ValidateNested({ each: true })
  @Type(() => CreateRunItemDto)
  items: CreateRunItemDto[];

  // Optional for older clients and pending requests; missing means all zero.
  @IsOptional()
  @ValidateNested()
  @Type(() => CreateRunElifDto)
  elif?: CreateRunElifDto | null;
}
