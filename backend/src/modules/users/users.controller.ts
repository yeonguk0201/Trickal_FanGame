import { Controller, Get, Param, Query } from '@nestjs/common';
import { GetUserRunsQueryDto } from './dto/get-user-runs-query.dto';
import { UsersService } from './users.service';

@Controller('users')
export class UsersController {
  constructor(private readonly usersService: UsersService) {}

  @Get(':nickname/runs')
  async getRunHistory(
    @Param('nickname') nickname: string,
    @Query() query: GetUserRunsQueryDto,
  ) {
    const { data, meta } = await this.usersService.getRunHistory(
      nickname,
      query,
    );

    return { success: true, data, meta };
  }
}
