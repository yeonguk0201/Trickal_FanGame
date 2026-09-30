import type {
  ApiFailure,
  ApiSuccess,
  CharacterStatisticsDto,
  ClearRankingDto,
  CharacterProgressDto,
  FloorStatisticsDto,
  ItemStatisticsDto,
  RankingMetaDto,
  RankingType,
  RunDetailDto,
  RunHistoryDto,
  RunRankingDto,
  RunSummaryDto,
  SkillType,
  StatisticsOverviewDto,
  UpgradeSkillRequestDto,
  UserProfileDto,
  UserSearchResultDto,
} from "./meta-api-contract";

interface RunHistoryResponse extends ApiSuccess<RunSummaryDto[]> {
  meta: { page: number; limit: number; total: number; totalPages: number };
}

interface RankingResponse<T> extends ApiSuccess<T[]> {
  meta: RankingMetaDto;
}

export class ApiClientError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = "ApiClientError";
  }
}

const API_URL =
  process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ||
  "http://localhost:3001/api";

export async function getUserProfile(nickname: string): Promise<UserProfileDto> {
  return request<UserProfileDto>(`/users/${encodeURIComponent(nickname)}`);
}

export async function searchUsers(
  nickname: string,
): Promise<UserSearchResultDto[]> {
  return request<UserSearchResultDto[]>(
    `/users/search?q=${encodeURIComponent(nickname)}`,
  );
}

export async function getUserRuns(
  nickname: string,
  page: number | string = 1,
  limit = 20,
): Promise<RunHistoryDto> {
  const response = await requestEnvelope<RunHistoryResponse>(
    `/users/${encodeURIComponent(nickname)}/runs?page=${encodeURIComponent(page)}&limit=${limit}`,
  );
  return { runs: response.data, ...response.meta };
}

export async function upgradeCharacterSkill(
  nickname: string,
  characterId: string,
  skillType: SkillType,
  targetLevel: number,
): Promise<CharacterProgressDto> {
  const body: UpgradeSkillRequestDto = { targetLevel };
  return request<CharacterProgressDto>(
    `/users/${encodeURIComponent(nickname)}/characters/${encodeURIComponent(characterId)}/skills/${skillType}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    },
  );
}

export async function getRunDetail(runId: string): Promise<RunDetailDto> {
  return request<RunDetailDto>(`/runs/${encodeURIComponent(runId)}`);
}

export async function getStatisticsOverview(): Promise<StatisticsOverviewDto> {
  return request<StatisticsOverviewDto>("/statistics");
}

export async function getCharacterStatistics(): Promise<CharacterStatisticsDto[]> {
  return request<CharacterStatisticsDto[]>("/statistics/characters");
}

export async function getItemStatistics(): Promise<ItemStatisticsDto[]> {
  return request<ItemStatisticsDto[]>("/statistics/items");
}

export async function getFloorStatistics(): Promise<FloorStatisticsDto[]> {
  return request<FloorStatisticsDto[]>("/statistics/floors");
}

export async function getRankings(
  type: "most-clears",
  limit?: number,
): Promise<{ data: ClearRankingDto[]; meta: RankingMetaDto }>;
export async function getRankings(
  type: Exclude<RankingType, "most-clears">,
  limit?: number,
): Promise<{ data: RunRankingDto[]; meta: RankingMetaDto }>;
export async function getRankings(type: RankingType, limit = 10) {
  const response = await requestEnvelope<
    RankingResponse<RunRankingDto | ClearRankingDto>
  >(`/rankings?type=${encodeURIComponent(type)}&page=1&limit=${limit}`);
  return { data: response.data, meta: response.meta };
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await requestEnvelope<ApiSuccess<T>>(path, init);
  return response.data;
}

async function requestEnvelope<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_URL}${path}`, { cache: "no-store", ...init });
  } catch {
    throw new ApiClientError(
      "NETWORK_ERROR",
      "서버에 연결할 수 없습니다. 잠시 후 다시 시도해 주세요.",
      0,
    );
  }

  const payload = (await response.json().catch(() => null)) as
    | T
    | ApiFailure
    | null;
  if (!response.ok) {
    const failure = payload as ApiFailure | null;
    throw new ApiClientError(
      failure?.error?.code || "API_ERROR",
      failure?.error?.message || "요청을 처리하지 못했습니다.",
      response.status,
    );
  }
  if (!payload) {
    throw new ApiClientError(
      "INVALID_RESPONSE",
      "서버 응답을 읽을 수 없습니다.",
      response.status,
    );
  }
  return payload as T;
}

export function getErrorMessage(error: unknown): string {
  return error instanceof Error
    ? error.message
    : "알 수 없는 오류가 발생했습니다.";
}
