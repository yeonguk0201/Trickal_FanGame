import { HttpStatus } from '@nestjs/common';
import type { Response } from 'express';

jest.mock('../../database/prisma.service', () => ({
  PrismaService: class PrismaService {},
}));

import type { CreateRunDto } from './dto/create-run.dto';
import { RunsController } from './runs.controller';
import type { RunsService } from './runs.service';

describe('RunsController', () => {
  const dto = {} as CreateRunDto;
  const data = {
    runId: 'run-id',
    experienceGained: 400,
    progress: {
      characterId: 'erpin',
      level: 2,
      experience: 0,
      experienceToNextLevel: 500,
      skillPoints: 1,
      lowGradeSkillLevel: 1,
      highGradeSkillLevel: 1,
    },
  };

  it.each([
    [true, HttpStatus.CREATED],
    [false, HttpStatus.OK],
  ])(
    'returns the contract body with the correct created=%s status',
    async (created, status) => {
      const service = {
        create: jest.fn().mockResolvedValue({ ...data, created }),
      };
      const controller = new RunsController(service as unknown as RunsService);
      const statusMock = jest.fn();
      const response = { status: statusMock } as unknown as Response;

      await expect(controller.create(dto, response)).resolves.toEqual({
        success: true,
        data,
      });
      expect(statusMock).toHaveBeenCalledWith(status);
    },
  );

  it('wraps a Run detail in the common response envelope', async () => {
    const detail = { id: 'run-id', items: [] };
    const service = {
      getDetail: jest.fn().mockResolvedValue(detail),
    };
    const controller = new RunsController(service as unknown as RunsService);

    await expect(controller.getDetail('run-id')).resolves.toEqual({
      success: true,
      data: detail,
    });
    expect(service.getDetail).toHaveBeenCalledWith('run-id');
  });
});
