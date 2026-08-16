import { Module } from '@nestjs/common';
import { ConfigModule } from '@nestjs/config';
import { AppController } from './app.controller';
import { AppService } from './app.service';
import { HealthController } from './health/health.controller';
import { HealthService } from './health/health.service';
import { PrismaModule } from './database/prisma.module';
import { RunsModule } from './modules/runs/runs.module';

@Module({
  imports: [ConfigModule.forRoot({ isGlobal: true }), PrismaModule, RunsModule],
  controllers: [AppController, HealthController],
  providers: [AppService, HealthService],
})
export class AppModule {}
