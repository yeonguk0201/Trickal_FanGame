import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import RankingsDashboard, { type RankingState } from "./rankings-dashboard";

const apiMocks = vi.hoisted(() => ({ getRankings: vi.fn() }));

vi.mock("@/lib/api-client", () => ({
  ...apiMocks,
  getErrorMessage: (error: unknown) => error instanceof Error ? error.message : "알 수 없는 오류",
}));

const runRanking = {
  rank: 1,
  nickname: "이름이 아주 긴 테스트 사용자 닉네임",
  runId: "run-test",
  character: { id: "erpin", name: "에르핀" },
  reachedFloor: 3,
  playTime: 600,
  endedAt: "2026-09-04T00:00:00.000Z",
};

describe("RankingsDashboard states", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders an empty message for every ranking", () => {
    render(<RankingsDashboard initialState={state()} />);

    expect(screen.getAllByText("표시할 랭킹 기록이 없습니다.")).toHaveLength(3);
  });

  it("keeps successful rankings and long names visible during a partial failure", () => {
    render(<RankingsDashboard initialState={state({
      highest: [runRanking],
      fastest: null,
      errors: { fastest: "클리어 타임 랭킹을 불러오지 못했습니다." },
    })} />);

    expect(screen.getByRole("link", { name: runRanking.nickname })).toBeInTheDocument();
    expect(screen.getByRole("alert")).toHaveTextContent("클리어 타임 랭킹을 불러오지 못했습니다.");
    expect(screen.getAllByText("표시할 랭킹 기록이 없습니다.")).toHaveLength(1);
  });

  it("recovers every ranking when retry succeeds after a total failure", async () => {
    let resolveHighest!: (value: { data: (typeof runRanking)[]; meta: ReturnType<typeof rankingMeta> }) => void;
    apiMocks.getRankings
      .mockReturnValueOnce(new Promise((resolve) => { resolveHighest = resolve; }))
      .mockResolvedValueOnce({ data: [], meta: rankingMeta("fastest-clear") })
      .mockResolvedValueOnce({ data: [], meta: rankingMeta("most-clears") });
    render(<RankingsDashboard initialState={state({
      highest: null,
      fastest: null,
      clears: null,
      errors: {
        highest: "서버 연결 실패",
        fastest: "서버 연결 실패",
        clears: "서버 연결 실패",
      },
    })} />);

    fireEvent.click(screen.getByRole("button", { name: "다시 시도" }));

    expect(screen.getByRole("heading", { name: "랭킹을 불러오는 중입니다…" })).toBeInTheDocument();
    resolveHighest({ data: [runRanking], meta: rankingMeta("highest-floor") });
    expect(await screen.findByRole("heading", { name: "랭킹" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: runRanking.nickname })).toBeInTheDocument();
    expect(apiMocks.getRankings).toHaveBeenCalledTimes(3);
  });
});

function state(overrides: Partial<RankingState> = {}): RankingState {
  return { highest: [], fastest: [], clears: [], errors: {}, ...overrides };
}

function rankingMeta(type: "highest-floor" | "fastest-clear" | "most-clears") {
  return { type, page: 1, limit: 10, total: 0, totalPages: 1 };
}
