import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import StatisticsDashboard, { type StatisticsState } from "./statistics-dashboard";

const apiMocks = vi.hoisted(() => ({
  getStatisticsOverview: vi.fn(),
  getCharacterStatistics: vi.fn(),
  getItemStatistics: vi.fn(),
  getFloorStatistics: vi.fn(),
}));

vi.mock("@/lib/api-client", () => ({
  ...apiMocks,
  getErrorMessage: (error: unknown) => error instanceof Error ? error.message : "알 수 없는 오류",
}));

const zeroOverview = {
  totalUsers: 0,
  totalRuns: 0,
  totalClears: 0,
  clearRate: 0,
  averagePlayTime: 0,
  averageReachedFloor: 0,
  highestReachedFloor: 0,
};

describe("StatisticsDashboard states", () => {
  beforeEach(() => vi.clearAllMocks());

  it("renders zero summary and explicit empty states without a database", () => {
    render(<StatisticsDashboard initialState={state()} />);

    expect(screen.getAllByText("0회")).toHaveLength(2);
    expect(screen.getByText("집계할 캐릭터 데이터가 없습니다.")).toBeInTheDocument();
    expect(screen.getByText("집계할 아티팩트 데이터가 없습니다.")).toBeInTheDocument();
    expect(screen.getByText("집계할 층 데이터가 없습니다.")).toBeInTheDocument();
  });

  it("keeps available sections and long names visible during a partial failure", () => {
    const longName = "아주 긴 이름의 테스트용 아티팩트가 화면 경계를 넘어가지 않는지 확인";
    render(<StatisticsDashboard initialState={state({
      characters: [{
        characterId: "erpin",
        characterName: "에르핀",
        totalRuns: 1,
        clears: 0,
        clearRate: 0,
        averageReachedFloor: 1,
        averagePlayTime: 30,
      }],
      items: [{
        itemId: "item-long-name",
        itemName: longName,
        acquiredRunCount: 1,
        selectionRate: 100,
        clearCountAfterAcquisition: 0,
        clearRateAfterAcquisition: 0,
      }],
      floors: null,
      errors: { floors: "층 통계를 불러오지 못했습니다." },
    })} />);

    expect(screen.getAllByText(longName)).toHaveLength(2);
    expect(screen.getByRole("alert")).toHaveTextContent("층 통계를 불러오지 못했습니다.");
    expect(screen.getAllByText("에르핀")).toHaveLength(2);
  });

  it("recovers all sections when retry succeeds after a total failure", async () => {
    let resolveOverview!: (value: typeof zeroOverview) => void;
    apiMocks.getStatisticsOverview.mockReturnValue(new Promise((resolve) => {
      resolveOverview = resolve;
    }));
    apiMocks.getCharacterStatistics.mockResolvedValue([]);
    apiMocks.getItemStatistics.mockResolvedValue([]);
    apiMocks.getFloorStatistics.mockResolvedValue([]);
    render(<StatisticsDashboard initialState={state({
      overview: null,
      characters: null,
      items: null,
      floors: null,
      errors: {
        overview: "서버 연결 실패",
        characters: "서버 연결 실패",
        items: "서버 연결 실패",
        floors: "서버 연결 실패",
      },
    })} />);

    fireEvent.click(screen.getByRole("button", { name: "다시 시도" }));

    expect(screen.getByRole("heading", { name: "통계를 불러오는 중입니다…" })).toBeInTheDocument();
    resolveOverview(zeroOverview);
    expect(await screen.findByRole("heading", { name: "전체 통계" })).toBeInTheDocument();
    expect(screen.queryByText("통계를 불러오지 못했습니다")).not.toBeInTheDocument();
    expect(apiMocks.getStatisticsOverview).toHaveBeenCalledOnce();
  });
});

function state(overrides: Partial<StatisticsState> = {}): StatisticsState {
  return {
    overview: zeroOverview,
    characters: [],
    items: [],
    floors: [],
    errors: {},
    ...overrides,
  };
}
