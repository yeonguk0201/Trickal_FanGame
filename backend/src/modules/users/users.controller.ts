import {
  Body,
  Controller,
  Get,
  HttpCode,
  HttpStatus,
  Param,
  Post,
  Put,
  Query,
  Res,
} from '@nestjs/common';
import type { Response } from 'express';
import { CreateUserDto } from './dto/create-user.dto';
import { GetUserRunsQueryDto } from './dto/get-user-runs-query.dto';
import { SearchUsersQueryDto } from './dto/search-users-query.dto';
import { UpgradeSkillDto } from './dto/upgrade-skill.dto';
import { UsersService } from './users.service';

@Controller('users')
export class UsersController {
  constructor(private readonly usersService: UsersService) {}

  @Post()
  @HttpCode(HttpStatus.CREATED)
  async createUser(
    @Body() dto: CreateUserDto,
    @Res({ passthrough: true }) res: Response,
  ) {
    const result = await this.usersService.createUser(dto);

    if (!result.isNew) {
      res.status(HttpStatus.OK);
    }

    return {
      success: true,
      data: {
        id: result.id,
        clientProfileId: result.clientProfileId,
        nickname: result.nickname,
        characterProgress: result.characterProgress.map((cp) => ({
          characterId: cp.characterId,
          level: cp.level,
          experience: cp.experience,
          experienceToNextLevel: result.experienceToNextLevel,
          skillPoints: cp.skillPoints,
          lowGradeSkillLevel: cp.lowGradeSkillLevel,
          highGradeSkillLevel: cp.highGradeSkillLevel,
        })),
      },
    };
  }

  @Get('search')
  async searchUsers(@Query() query: SearchUsersQueryDto) {
    const data = await this.usersService.searchUsers(query.q);

    return { success: true, data };
  }

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
