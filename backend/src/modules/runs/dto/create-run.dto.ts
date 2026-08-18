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
  MaxLength,
  Min,
  ValidateNested,
} from 'class-validator';

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
}

export class CreateRunDto {
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
}
