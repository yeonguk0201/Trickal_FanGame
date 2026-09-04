"use client";

import Link from "next/link";
import { useState } from "react";
import { getErrorMessage, getRankings } from "@/lib/api-client";
import type { ClearRankingDto, RunRankingDto } from "@/lib/meta-api-contract";
import styles from "./rankings.module.css";
import shared from "../statistics/statistics.module.css";

export type RankingState = {
  highest: RunRankingDto[] | null;
  fastest: RunRankingDto[] | null;
  clears: ClearRankingDto[] | null;
  errors: Partial<Record<"highest" | "fastest" | "clears", string>>;
};

const emptyState: RankingState = { highest: null, fastest: null, clears: null, errors: {} };

export default function RankingsDashboard({ initialState }: { initialState: RankingState }) {
  const [state, setState] = useState<RankingState>(initialState);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    const [highest, fastest, clears] = await Promise.allSettled([
      getRankings("highest-floor"),
      getRankings("fastest-clear"),
      getRankings("most-clears"),
    ]);
    const next: RankingState = { ...emptyState, errors: {} };
    if (highest.status === "fulfilled") next.highest = highest.value.data;
    else next.errors.highest = getErrorMessage(highest.reason);
    if (fastest.status === "fulfilled") next.fastest = fastest.value.data;
    else next.errors.fastest = getErrorMessage(fastest.reason);
    if (clears.status === "fulfilled") next.clears = clears.value.data;
    else next.errors.clears = getErrorMessage(clears.reason);
    setState(next);
    setLoading(false);
  };

  if (loading) return <Status title="랭킹을 불러오는 중입니다…" />;
  if (Object.keys(state.errors).length === 3) {
    return <Status title="랭킹을 불러오지 못했습니다" message={state.errors.highest} action={<button onClick={() => void load()}>다시 시도</button>} />;
  }

  return (
    <main className={shared.page}>
      <header className={shared.header}>
        <nav aria-label="주요 메뉴"><Link href="/">홈</Link><Link href="/statistics">통계</Link></nav>
        <p className={shared.eyebrow}>HALL OF FAME</p>
        <h1>랭킹</h1>
        <p>각 유저의 최고 기록 하나를 기준으로 순위를 표시합니다. 같은 기준 값은 API의 안정적인 보조 정렬 순서를 따릅니다.</p>
      </header>
      <RankingSection title="최고 도달 층" subtitle="층 · 빠른 시간 순" error={state.errors.highest} retry={load}>
        <RunRankingTable rows={state.highest} value={(row) => `${row.reachedFloor}층`} />
      </RankingSection>
      <RankingSection title="클리어 타임" subtitle="클리어 Run · 빠른 시간 순" error={state.errors.fastest} retry={load}>
        <RunRankingTable rows={state.fastest} value={(row) => formatTime(row.playTime)} />
      </RankingSection>
      <RankingSection title="클리어 횟수" subtitle="클리어 수 · 총 플레이 순" error={state.errors.clears} retry={load}>
        {state.clears && state.clears.length > 0 ? <div className={styles.list}>{state.clears.map((row) => <article className={styles.rankCard} key={row.nickname}><Rank rank={row.rank} /><div><Link href={`/users/${encodeURIComponent(row.nickname)}`}>{row.nickname}</Link><span>총 {row.totalRuns}회 플레이</span></div><strong>{row.clears}회</strong></article>)}</div> : <Empty />}
      </RankingSection>
    </main>
  );
}

function RankingSection({ title, subtitle, error, retry, children }: { title: string; subtitle: string; error?: string; retry: () => Promise<void>; children: React.ReactNode }) {
  return <section className={shared.section}><div className={shared.sectionHeading}><div><p className={shared.eyebrow}>{subtitle}</p><h2>{title}</h2></div></div>{error ? <div className={shared.error} role="alert"><p>{error}</p><button onClick={() => void retry()}>다시 시도</button></div> : children}</section>;
}

function RunRankingTable({ rows, value }: { rows: RunRankingDto[] | null; value: (row: RunRankingDto) => string }) {
  if (!rows || rows.length === 0) return <Empty />;
  return <div className={styles.list}>{rows.map((row) => <article className={styles.rankCard} key={row.runId}><Rank rank={row.rank} /><div><Link href={`/users/${encodeURIComponent(row.nickname)}`}>{row.nickname}</Link><span>{row.character.name} · <Link className={styles.detailLink} href={`/runs/${row.runId}`}>Run 상세</Link></span></div><strong>{value(row)}</strong></article>)}</div>;
}

function Rank({ rank }: { rank: number }) { return <span className={styles.rank} aria-label={`${rank}위`}>{rank}</span>; }
function Empty() { return <div className={shared.empty}>표시할 랭킹 기록이 없습니다.</div>; }
function Status({ title, message, action }: { title: string; message?: string; action?: React.ReactNode }) { return <main className={shared.statusPage}><p className={shared.eyebrow}>HALL OF FAME</p><h1>{title}</h1>{message && <p>{message}</p>}{action}<Link href="/">홈으로</Link></main>; }
function formatTime(seconds: number) { const rounded = Math.round(seconds); return `${Math.floor(rounded / 60)}:${String(rounded % 60).padStart(2, "0")}`; }
