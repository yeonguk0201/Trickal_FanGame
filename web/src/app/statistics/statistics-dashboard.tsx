"use client";

import Link from "next/link";
import { useState } from "react";
import {
  getCharacterStatistics,
  getErrorMessage,
  getFloorStatistics,
  getItemStatistics,
  getStatisticsOverview,
} from "@/lib/api-client";
import type {
  CharacterStatisticsDto,
  FloorStatisticsDto,
  ItemStatisticsDto,
  StatisticsOverviewDto,
} from "@/lib/meta-api-contract";
import styles from "./statistics.module.css";

export type StatisticsState = {
  overview: StatisticsOverviewDto | null;
  characters: CharacterStatisticsDto[] | null;
  items: ItemStatisticsDto[] | null;
  floors: FloorStatisticsDto[] | null;
  errors: Partial<Record<"overview" | "characters" | "items" | "floors", string>>;
};

const emptyState: StatisticsState = {
  overview: null,
  characters: null,
  items: null,
  floors: null,
  errors: {},
};

export default function StatisticsDashboard({ initialState }: { initialState: StatisticsState }) {
  const [state, setState] = useState<StatisticsState>(initialState);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    const results = await Promise.allSettled([
      getStatisticsOverview(),
      getCharacterStatistics(),
      getItemStatistics(),
      getFloorStatistics(),
    ]);
    const keys = ["overview", "characters", "items", "floors"] as const;
    const next: StatisticsState = { ...emptyState, errors: {} };
    results.forEach((result, index) => {
      const key = keys[index];
      if (result.status === "fulfilled") {
        Object.assign(next, { [key]: result.value });
      } else {
        next.errors[key] = getErrorMessage(result.reason);
      }
    });
    setState(next);
    setLoading(false);
  };

  if (loading) {
    return (
      <main className={styles.statusPage} aria-live="polite">
        <p className={styles.eyebrow}>DATA ARCHIVE</p>
        <h1>통계를 불러오는 중입니다…</h1>
      </main>
    );
  }

  const allFailed = Object.keys(state.errors).length === 4;
  if (allFailed) {
    return (
      <main className={styles.statusPage}>
        <p className={styles.eyebrow}>DATA ARCHIVE</p>
        <h1>통계를 불러오지 못했습니다</h1>
        <p>{state.errors.overview}</p>
        <button onClick={() => void load()}>다시 시도</button>
        <Link href="/">홈으로</Link>
      </main>
    );
  }

  return (
    <main className={styles.page}>
      <PageHeader eyebrow="GAME DATA" title="전체 통계" description="저장된 모든 Run을 같은 기준으로 비교합니다." />

      {state.overview ? (
        <section aria-labelledby="overview-title">
          <h2 id="overview-title" className={styles.visuallyHidden}>전체 요약</h2>
          <div className={styles.summaryGrid}>
            <Summary label="플레이" value={`${state.overview.totalRuns}회`} />
            <Summary label="클리어" value={`${state.overview.totalClears}회`} />
            <Summary label="클리어율" value={percent(state.overview.clearRate)} />
            <Summary label="최고 도달" value={`${state.overview.highestReachedFloor}층`} />
            <Summary label="평균 도달" value={`${state.overview.averageReachedFloor.toFixed(1)}층`} />
            <Summary label="평균 시간" value={formatTime(state.overview.averagePlayTime)} />
          </div>
        </section>
      ) : <SectionError message={state.errors.overview} retry={load} />}

      <section className={styles.explainer} aria-labelledby="criteria-title">
        <h2 id="criteria-title">데이터를 읽는 기준</h2>
        <p>모든 비율은 전체 저장 Run을 기준으로 계산하며, 분모가 0이면 0%로 표시합니다.</p>
        <p>아티팩트 선택은 같은 Run에서 여러 번 얻어도 1회로 셉니다. 층별 사망률과 클리어율의 분모는 해당 층에 도달한 Run입니다.</p>
      </section>

      <StatisticsSection title="캐릭터 통계" description="플레이 횟수와 클리어율" error={state.errors.characters} retry={load}>
        {state.characters && state.characters.length > 0 ? (
          <>
            <BarChart label="캐릭터별 플레이 횟수" rows={state.characters.map((item) => ({ label: item.characterName, value: item.totalRuns, display: `${item.totalRuns}회` }))} />
            <div className={styles.tableWrap}><table><thead><tr><th>캐릭터</th><th>플레이</th><th>클리어율</th><th>평균 도달</th></tr></thead><tbody>{state.characters.map((item) => <tr key={item.characterId}><th>{item.characterName}</th><td>{item.totalRuns}회</td><td>{percent(item.clearRate)}</td><td>{item.averageReachedFloor.toFixed(1)}층</td></tr>)}</tbody></table></div>
          </>
        ) : <EmptyState text="집계할 캐릭터 데이터가 없습니다." />}
      </StatisticsSection>

      <StatisticsSection title="아티팩트 통계" description="선택률과 획득 후 클리어율" error={state.errors.items} retry={load}>
        {state.items && state.items.length > 0 ? (
          <>
            <BarChart label="아티팩트 선택률" rows={state.items.map((item) => ({ label: item.itemName, value: item.selectionRate, display: percent(item.selectionRate) }))} max={100} />
            <div className={styles.tableWrap}><table><thead><tr><th>아티팩트</th><th>선택 Run</th><th>선택률</th><th>획득 후 클리어율</th></tr></thead><tbody>{state.items.map((item) => <tr key={item.itemId}><th>{item.itemName}</th><td>{item.acquiredRunCount}회</td><td>{percent(item.selectionRate)}</td><td>{percent(item.clearRateAfterAcquisition)}</td></tr>)}</tbody></table></div>
          </>
        ) : <EmptyState text="집계할 아티팩트 데이터가 없습니다." />}
      </StatisticsSection>

      <StatisticsSection title="층 통계" description="도달률·사망률·클리어율" error={state.errors.floors} retry={load}>
        {state.floors && state.floors.length > 0 ? (
          <>
            <BarChart label="층별 도달률" rows={state.floors.map((item) => ({ label: `${item.floor}층`, value: item.reachRate, display: percent(item.reachRate) }))} max={100} />
            <div className={styles.tableWrap}><table><thead><tr><th>층</th><th>도달 Run</th><th>도달률</th><th>사망률</th><th>클리어율</th></tr></thead><tbody>{state.floors.map((item) => <tr key={item.floor}><th>{item.floor}층</th><td>{item.reachedRunCount}회</td><td>{percent(item.reachRate)}</td><td>{percent(item.deathRate)}</td><td>{percent(item.clearRate)}</td></tr>)}</tbody></table></div>
          </>
        ) : <EmptyState text="집계할 층 데이터가 없습니다." />}
      </StatisticsSection>
    </main>
  );
}

function PageHeader({ eyebrow, title, description }: { eyebrow: string; title: string; description: string }) {
  return <header className={styles.header}><nav aria-label="주요 메뉴"><Link href="/">홈</Link><Link href="/rankings">랭킹</Link></nav><p className={styles.eyebrow}>{eyebrow}</p><h1>{title}</h1><p>{description}</p></header>;
}

function Summary({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

function StatisticsSection({ title, description, error, retry, children }: { title: string; description: string; error?: string; retry: () => Promise<void>; children: React.ReactNode }) {
  return <section className={styles.section}><div className={styles.sectionHeading}><div><p className={styles.eyebrow}>{description}</p><h2>{title}</h2></div></div>{error ? <SectionError message={error} retry={retry} /> : children}</section>;
}

function SectionError({ message, retry }: { message?: string; retry: () => Promise<void> }) {
  return <div className={styles.error} role="alert"><p>{message ?? "데이터를 불러오지 못했습니다."}</p><button onClick={() => void retry()}>다시 시도</button></div>;
}

function EmptyState({ text }: { text: string }) {
  return <div className={styles.empty}>{text}</div>;
}

function BarChart({ label, rows, max }: { label: string; rows: { label: string; value: number; display: string }[]; max?: number }) {
  const scale = max ?? Math.max(1, ...rows.map((row) => row.value));
  return <div className={styles.chart} role="img" aria-label={label}>{rows.map((row) => <div className={styles.barRow} key={row.label}><span title={row.label}>{row.label}</span><div className={styles.track}><i style={{ width: `${Math.min(100, (row.value / scale) * 100)}%` }} /></div><strong>{row.display}</strong></div>)}</div>;
}

function percent(value: number) { return `${value.toFixed(1)}%`; }
function formatTime(seconds: number) { const rounded = Math.round(seconds); return `${Math.floor(rounded / 60)}:${String(rounded % 60).padStart(2, "0")}`; }
