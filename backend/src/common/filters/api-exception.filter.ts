import {
  ArgumentsHost,
  Catch,
  ExceptionFilter,
  HttpException,
  HttpStatus,
} from '@nestjs/common';
import type { Response } from 'express';

type ApiErrorResponse = {
  success: false;
  error: {
    code: string;
    message: string;
  };
};

@Catch()
export class ApiExceptionFilter implements ExceptionFilter {
  catch(exception: unknown, host: ArgumentsHost) {
    const response = host.switchToHttp().getResponse<Response>();
    const status =
      exception instanceof HttpException
        ? exception.getStatus()
        : HttpStatus.INTERNAL_SERVER_ERROR;

    response.status(status).json(this.toResponse(exception, status));
  }

  private toResponse(exception: unknown, status: number): ApiErrorResponse {
    const isServerError = status >= 500;

    if (exception instanceof HttpException) {
      const body = exception.getResponse();

      if (this.isApiErrorResponse(body)) {
        return body;
      }
    }

    return {
      success: false,
      error: {
        code: isServerError ? 'INTERNAL_SERVER_ERROR' : 'INVALID_REQUEST',
        message: isServerError
          ? '서버 내부 오류가 발생했습니다.'
          : '요청을 처리할 수 없습니다.',
      },
    };
  }

  private isApiErrorResponse(value: unknown): value is ApiErrorResponse {
    if (typeof value !== 'object' || value === null) {
      return false;
    }

    const candidate = value as Partial<ApiErrorResponse>;
    return (
      candidate.success === false &&
      typeof candidate.error?.code === 'string' &&
      typeof candidate.error.message === 'string'
    );
  }
}
