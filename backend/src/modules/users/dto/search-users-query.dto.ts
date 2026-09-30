import { Transform } from 'class-transformer';
import { IsString, Length } from 'class-validator';

export class SearchUsersQueryDto {
  @Transform(({ value }: { value: unknown }) =>
    typeof value === 'string' ? value.trim() : value,
  )
  @IsString()
  @Length(2, 50)
  q!: string;
}
