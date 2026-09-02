"use client";

import Link from "next/link";
import { useState } from "react";
import { getErrorMessage, getRunDetail } from "@/lib/api-client";
import type { RunDetailDto } from "@/lib/meta-api-contract";
import styles from "./run-detail.module.css";

const RARITY_LABEL: Record<string, string> = {
  COMMON: "일반",
  UNCOMMON: "고급",
  RARE: "희귀",
  EPIC: "전설",
};

export default function RunDetail({ runId, initialRun, initialError = null }: {
  runId: string;
  initialRun: RunDetailDto | null;
  initialError?: string | null;
}) {
  const [run, setRun] = useState<RunDetailDto | null>(initialRun);
  const [error, setError] = useState<string | null>(initialError);
  const [loading, setLoading] = useState(false);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      setRun(await getRunDetail(runId));
    } catch (reason) {
      setRun(null);
      setError(getErrorMessage(reason));
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return <main className={styles.status}>Run 상세를 불러오는 중입니다…</main>;
  }
  if (!run) {
    return (
      <main className={styles.status}>
        <h1>Run을 불러오지 못했습니다</h1>
        <p>{error}</p>
        <button onClick={() => void load()}>다시 시도</button>
        <Link href="/">홈으로</Link>
      </main>
    );
  }

  const orderedItems = [...run.items].sort((a, b) => a.order - b.order);
  return (
    <main className={styles.page}>
      <Link href={`/users/${encodeURIComponent(run.user.nickname)}`} className={styles.back}>
        ← {run.user.nickname}의 전적으로
      </Link>
      <header className={styles.hero}>
        <span className={run.isCleared ? styles.clear : styles.death}>
          {run.isCleared ? "RUN CLEARED" : "RUN ENDED"}
        </span>
        <h1>{run.character.name}의 {run.reachedFloor}층 도전</h1>
        <p>{formatDate(run.endedAt)} · 게임 버전 {run.gameVersion}</p>
      </header>

      <section className={styles.metrics} aria-label="Run 요약">
        <Metric label="결과" value={run.isCleared ? "클리어" : `사망 · ${deathLabel(run.deathReason)}`} />
        <Metric label="플레이 시간" value={formatPlayTime(run.playTime)} />
        <Metric label="도달 층" value={`${run.reachedFloor}층`} />
        <Metric label="처치 수" value={`${run.killCount}`} />
      </section>

      <section className={styles.items}>
        <div className={styles.heading}>
          <div>
            <p>ACQUISITION LOG</p>
            <h2>아티팩트 획득 순서</h2>
          </div>
          <strong>{orderedItems.length}개</strong>
        </div>
        {orderedItems.length === 0 ? (
          <div className={styles.empty}>이 Run에서 획득한 아티팩트가 없습니다.</div>
        ) : (
          <ol className={styles.timeline}>
            {orderedItems.map((item) => (
              <li key={`${item.order}-${item.itemId}`}>
                <span className={styles.order}>{item.order}</span>
                <div>
                  <h3>{item.name}</h3>
                  <p>{item.itemId} · {RARITY_LABEL[item.rarity] ?? item.rarity}</p>
                </div>
                <span className={styles.floor}>{item.floor}층</span>
                <time dateTime={item.acquiredAt}>{formatTime(item.acquiredAt)}</time>
              </li>
            ))}
          </ol>
        )}
      </section>
    </main>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

function deathLabel(reason: string | null): string {
  const labels: Record<string, string> = {
    MONSTER: "몬스터",
    BOSS: "보스",
    ENVIRONMENT: "환경",
    UNKNOWN: "원인 불명",
  };
  return reason ? labels[reason] ?? reason : "원인 불명";
}

function formatPlayTime(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}분 ${seconds.toString().padStart(2, "0")}초`;
}

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat("ko-KR", {
    dateStyle: "long",
    timeStyle: "short",
    timeZone: "Asia/Seoul",
  }).format(new Date(iso));
}

function formatTime(iso: string): string {
  return new Intl.DateTimeFormat("ko-KR", {
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
    timeZone: "Asia/Seoul",
  }).format(new Date(iso));
}
