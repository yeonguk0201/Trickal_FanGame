import { Body, Controller, Get, Param, Put, Query } from '@nestjs/common';
import { GetUserRunsQueryDto } from './dto/get-user-runs-query.dto';
import { UpgradeSkillDto } from './dto/upgrade-skill.dto';
import { UsersService } from './users.service';

@Controller('users')
export class UsersController {
  constructor(private readonly usersService: UsersService) {}

  @Get(':nickname')
  async getUser(@Param('nickname') nickname: string) {
    const data = await this.usersService.getUser(nickname);

    return { success: true, data };
  }

  @Put(':nickname/characters/:characterId/skills/:skillType')
  async upgradeSkill(
    @Param('nickname') nickname: string,
    @Param('characterId') characterId: string,
    @Param('skillType') skillType: string,
    @Body() dto: UpgradeSkillDto,
  ) {
    const data = await this.usersService.upgradeSkill(
      nickname,
      characterId,
      skillType,
      dto.targetLevel,
    );

    return { success: true, data };
  }

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
