import { HttpStatus, INestApplication, ValidationPipe } from '@nestjs/common';
import { Test } from '@nestjs/testing';
import request from 'supertest';
import { ApiException } from '../../common/exceptions/api.exception';
import { ApiExceptionFilter } from '../../common/filters/api-exception.filter';
import { UsersController } from './users.controller';
import { UsersService } from './users.service';

describe('Users search HTTP contract', () => {
  let app: INestApplication;
  const searchUsers = jest.fn();

  beforeEach(async () => {
    const module = await Test.createTestingModule({
      controllers: [UsersController],
      providers: [{ provide: UsersService, useValue: { searchUsers } }],
    }).compile();

    app = module.createNestApplication();
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
    await app.init();
  });

  afterEach(async () => {
    jest.resetAllMocks();
    await app.close();
  });

  it('routes /users/search before the nickname route and trims q', async () => {
    searchUsers.mockResolvedValue([{ nickname: 'test-player' }]);

    await request(app.getHttpServer())
      .get('/users/search?q=%20%20test-player%20%20')
      .expect(200)
      .expect({ success: true, data: [{ nickname: 'test-player' }] });
    expect(searchUsers).toHaveBeenCalledWith('test-player');
  });

  it('returns an empty array for an unknown exact nickname', async () => {
    searchUsers.mockResolvedValue([]);

    await request(app.getHttpServer())
      .get('/users/search?q=missing')
      .expect(200)
      .expect({ success: true, data: [] });
  });

  it.each(['/users/search', '/users/search?q=', '/users/search?q=a'])(
    'returns the validation contract for invalid input: %s',
    async (url) => {
      await request(app.getHttpServer())
        .get(url)
        .expect(422)
        .expect({
          success: false,
          error: {
            code: 'VALIDATION_ERROR',
            message: '요청 데이터가 유효하지 않습니다.',
          },
        });
      expect(searchUsers).not.toHaveBeenCalled();
    },
  );

  it('returns the common server error without leaking database details', async () => {
    searchUsers.mockRejectedValue(new Error('database unavailable'));

    await request(app.getHttpServer())
      .get('/users/search?q=test-player')
      .expect(500)
      .expect({
        success: false,
        error: {
          code: 'INTERNAL_SERVER_ERROR',
          message: '서버 내부 오류가 발생했습니다.',
        },
      });
  });
});
