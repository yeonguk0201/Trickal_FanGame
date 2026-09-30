import { HttpException, HttpStatus } from '@nestjs/common';

export class ApiException extends HttpException {
  constructor(status: HttpStatus, code: string, message: string) {
    super(
      {
        success: false,
        error: { code, message },
      },
      status,
    );
  }
}
