import {
  createParamDecorator,
  HttpStatus,
  type ExecutionContext,
} from '@nestjs/common';
import { isUUID } from 'class-validator';
import type { Request } from 'express';
import { ApiException } from '../exceptions/api.exception';

// The game creates clientProfileId once per install and keeps it secret.
// Write requests send it here so the server can check who owns the profile.
export const CLIENT_PROFILE_KEY_HEADER = 'x-client-profile-id';

export function readClientProfileKey(value: unknown): string {
  const key: unknown = Array.isArray(value) ? (value as unknown[])[0] : value;
  if (typeof key !== 'string' || !isUUID(key)) {
    throw new ApiException(
      HttpStatus.UNAUTHORIZED,
      'PROFILE_KEY_REQUIRED',
      '프로필 키가 필요합니다.',
    );
  }

  return key.toLowerCase();
}

export function profileKeyMismatch(): ApiException {
  return new ApiException(
    HttpStatus.FORBIDDEN,
    'PROFILE_KEY_MISMATCH',
    '이 프로필에 대한 권한이 없습니다.',
  );
}

export const ClientProfileKey = createParamDecorator(
  (_data: unknown, context: ExecutionContext): string =>
    readClientProfileKey(
      context.switchToHttp().getRequest<Request>().headers[
        CLIENT_PROFILE_KEY_HEADER
      ],
    ),
);
