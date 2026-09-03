import { Controller, Get, Param } from '@nestjs/common';
import { StatisticsService } from './statistics.service';

@Controller('statistics')
export class StatisticsController {
  constructor(private readonly service: StatisticsService) {}

  @Get()
  async getOverview() {
    return { success: true, data: await this.service.getOverview() };
  }

  @Get('characters')
  async getCharacters() {
    return { success: true, data: await this.service.getCharacters() };
  }

  @Get('items')
  async getItems() {
    return { success: true, data: await this.service.getItems() };
  }

  @Get('floors')
  async getFloors() {
    return { success: true, data: await this.service.getFloors() };
  }

  @Get('users/:nickname')
  async getUser(@Param('nickname') nickname: string) {
    return { success: true, data: await this.service.getUser(nickname) };
  }
}
