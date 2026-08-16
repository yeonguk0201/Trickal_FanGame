import { Body, Controller, Post } from '@nestjs/common';
import { CreateRunDto } from './dto/create-run.dto';
import { RunsService } from './runs.service';

@Controller('runs')
export class RunsController {
  constructor(private readonly runsService: RunsService) {}

  @Post()
  async create(@Body() dto: CreateRunDto) {
    const runId = await this.runsService.create(dto);

    return {
      success: true,
      data: { runId },
    };
  }
}
