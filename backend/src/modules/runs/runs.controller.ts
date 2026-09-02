import {
  Body,
  Controller,
  Get,
  HttpStatus,
  Param,
  Post,
  Res,
} from '@nestjs/common';
import type { Response } from 'express';
import { CreateRunDto } from './dto/create-run.dto';
import { RunsService } from './runs.service';

@Controller('runs')
export class RunsController {
  constructor(private readonly runsService: RunsService) {}

  @Get(':runId')
  async getDetail(@Param('runId') runId: string) {
    const data = await this.runsService.getDetail(runId);

    return { success: true, data };
  }

  @Post()
  async create(
    @Body() dto: CreateRunDto,
    @Res({ passthrough: true }) response: Response,
  ) {
    const { created, ...data } = await this.runsService.create(dto);
    response.status(created ? HttpStatus.CREATED : HttpStatus.OK);

    return {
      success: true,
      data,
    };
  }
}
