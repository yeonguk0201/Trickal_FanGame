import { Transform } from 'class-transformer';
import { IsUUID, Matches, MaxLength, MinLength } from 'class-validator';

export class CreateUserDto {
  @IsUUID()
  clientProfileId: string;

  @Transform(({ value }: { value: unknown }) =>
    typeof value === 'string' ? value.trim() : value,
  )
  @MinLength(2, { message: '닉네임은 2자 이상이어야 합니다.' })
  @MaxLength(12, { message: '닉네임은 12자 이하여야 합니다.' })
  @Matches(/^[가-힣a-zA-Z0-9]+$/, {
    message: '닉네임은 한글, 영문, 숫자만 사용할 수 있습니다.',
  })
  nickname: string;
}
