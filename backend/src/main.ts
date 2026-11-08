import { NestFactory } from '@nestjs/core';
import { HttpStatus, ValidationPipe } from '@nestjs/common';
import { AppModule } from './app.module';
import { ApiException } from './common/exceptions/api.exception';
import { ApiExceptionFilter } from './common/filters/api-exception.filter';
import { resolveRuntimeConfig } from './config/runtime-config';

async function bootstrap() {
  const config = resolveRuntimeConfig(process.env);
  const app = await NestFactory.create(AppModule);

  app.setGlobalPrefix('api');
  app.enableCors({ origin: config.corsOrigins });
  // Hosting platforms stop the process with SIGTERM; close DB connections first.
  app.enableShutdownHooks();
  app.useGlobalFilters(new ApiExceptionFilter());
  app.useGlobalPipes(
    new ValidationPipe({
      transform: true,
      whitelist: true,
      forbidNonWhitelisted: true,
      exceptionFactory: () =>
        new ApiException(
          HttpStatus.UNPROCESSABLE_ENTITY,
          'VALIDATION_ERROR',
          '요청 데이터가 유효하지 않습니다.',
        ),
    }),
  );

  await app.listen(config.port);
}
void bootstrap();
