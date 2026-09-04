"use client";

import Link from "next/link";
import { useState } from "react";
import {
  ApiClientError,
  getErrorMessage,
  getUserProfile,
  getUserRuns,
  upgradeCharacterSkill,
} from "@/lib/api-client";
import type {
  CharacterProgressDto,
  RunHistoryDto,
  SkillType,
  UserProfileDto,
} from "@/lib/meta-api-contract";
import styles from "./user-dashboard.module.css";

export default function UserDashboard({
  nickname,
  requestedPage,
  initialUser,
  initialHistory,
  initialProfileError,
  initialProfileErrorCode,
  initialHistoryError,
  initialHistoryErrorCode,
}: {
  nickname: string;
  requestedPage: string;
  initialUser: UserProfileDto | null;
  initialHistory: RunHistoryDto | null;
  initialProfileError: string | null;
  initialProfileErrorCode: string | null;
  initialHistoryError: string | null;
  initialHistoryErrorCode: string | null;
}) {
  const [user, setUser] = useState<UserProfileDto | null>(initialUser);
  const [history, setHistory] = useState<RunHistoryDto | null>(initialHistory);
  const [profileError, setProfileError] = useState<string | null>(initialProfileError);
  const [profileErrorCode, setProfileErrorCode] = useState<string | null>(initialProfileErrorCode);
  const [historyError, setHistoryError] = useState<string | null>(initialHistoryError);
  const [historyErrorCode, setHistoryErrorCode] = useState<string | null>(initialHistoryErrorCode);
  const [isLoading, setIsLoading] = useState(false);

  const loadData = async () => {
    setIsLoading(true);
    setProfileError(null);
    setProfileErrorCode(null);
    setHistoryError(null);
    setHistoryErrorCode(null);
    const [profileResult, historyResult] = await Promise.allSettled([
      getUserProfile(nickname),
      getUserRuns(nickname, requestedPage, 10),
    ]);

    if (profileResult.status === "fulfilled") {
      setUser(profileResult.value);
    } else {
      setUser(null);
      setProfileError(getErrorMessage(profileResult.reason));
      setProfileErrorCode(
        profileResult.reason instanceof ApiClientError
          ? profileResult.reason.code
          : null,
      );
    }
    if (historyResult.status === "fulfilled") {
      setHistory(historyResult.value);
    } else {
      setHistory(null);
      setHistoryError(getErrorMessage(historyResult.reason));
      setHistoryErrorCode(
        historyResult.reason instanceof ApiClientError
          ? historyResult.reason.code
          : null,
      );
    }
    setIsLoading(false);
  };

  if (isLoading) {
    return <StatusPage title="기록을 불러오는 중입니다…" />;
  }

  if (!user) {
    const userMissing = profileErrorCode === "USER_NOT_FOUND";
    return (
      <StatusPage
        title={userMissing ? "존재하지 않는 사용자입니다" : "사용자 기록을 불러오지 못했습니다"}
        message={profileError ?? undefined}
        action={
          userMissing ? null : <button onClick={() => void loadData()}>다시 시도</button>
        }
      />
    );
  }

  return (
    <main className={styles.page}>
      <header className={styles.hero}>
        <Link href="/" className={styles.backLink}>
          ← 다른 사용자 찾기
        </Link>
        <nav className={styles.dataNav} aria-label="데이터 탐색">
          <Link href="/statistics">전체 통계</Link>
          <Link href="/rankings">랭킹</Link>
        </nav>
        <p className={styles.eyebrow}>교주의 모험 기록</p>
        <h1>{user.nickname}</h1>
        <div className={styles.stats}>
          <Stat label="플레이" value={`${user.stats.totalRuns}회`} />
          <Stat label="클리어" value={`${user.stats.clears}회`} />
          <Stat label="승률" value={`${user.stats.winRate.toFixed(1)}%`} />
          <Stat label="최고 층" value={`${user.stats.highestFloor}층`} />
          <Stat label="평균 도달 층" value={`${user.stats.averageFloor.toFixed(1)}층`} />
          <Stat label="평균 플레이 시간" value={formatPlayTime(user.stats.averagePlayTime)} />
        </div>
      </header>

      <section className={styles.section} aria-labelledby="progress-title">
        <div className={styles.sectionHeading}>
          <div>
            <p className={styles.eyebrow}>메타 성장</p>
            <h2 id="progress-title">캐릭터 진행</h2>
          </div>
        </div>
        {user.characterProgress.length === 0 ? (
          <EmptyState>아직 성장 기록이 없습니다. 첫 Run을 완료해 보세요.</EmptyState>
        ) : (
          <div className={styles.progressGrid}>
            {user.characterProgress.map((progress) => (
              <ProgressCard
                key={progress.characterId}
                nickname={nickname}
                progress={progress}
                onUpdate={(updated) =>
                  setUser((current) =>
                    current
                      ? {
                          ...current,
                          characterProgress: current.characterProgress.map(
                            (item) =>
                              item.characterId === updated.characterId
                                ? { ...item, ...updated }
                                : item,
                          ),
                        }
                      : current,
                  )
                }
              />
            ))}
          </div>
        )}
      </section>

      {history && history.runs.length > 0 && (
        <section className={styles.section} aria-labelledby="record-chart-title">
          <div className={styles.sectionHeading}>
            <div>
              <p className={styles.eyebrow}>현재 페이지 · 오래된 순</p>
              <h2 id="record-chart-title">최근 플레이 기록</h2>
            </div>
          </div>
          <div className={styles.runChart} aria-label="최근 Run별 도달 층">
            {[...history.runs].reverse().map((run, index) => (
              <Link
                key={run.runId}
                href={`/runs/${run.runId}?fromPage=${history.page}`}
                className={styles.runBar}
                aria-label={`${index + 1}번째 Run, ${run.reachedFloor}층 도달, ${run.isCleared ? "클리어" : "사망"}`}
              >
                <span className={styles.barValue}>{run.reachedFloor}층</span>
                <i
                  className={run.isCleared ? styles.clearBar : styles.deathBar}
                  style={{ height: `${Math.max(12, (run.reachedFloor / Math.max(1, ...history.runs.map((item) => item.reachedFloor))) * 100)}%` }}
                />
                <small>{index + 1}</small>
              </Link>
            ))}
          </div>
          <p className={styles.chartNote}>막대를 선택하면 Run 상세로 이동합니다. 노랑은 클리어, 빨강은 사망 기록입니다.</p>
        </section>
      )}

      <section className={styles.section} aria-labelledby="runs-title">
        <div className={styles.sectionHeading}>
          <div>
            <p className={styles.eyebrow}>페이지당 10개</p>
            <h2 id="runs-title">Run 전적</h2>
          </div>
          {history && <span>전체 {history.total}회</span>}
        </div>
        {historyError ? (
          <InlineError
            message={historyError}
            retry={() => void loadData()}
            firstPageHref={
              historyErrorCode === "RUN_PAGE_OUT_OF_RANGE" ||
              historyErrorCode === "VALIDATION_ERROR"
                ? `/users/${encodeURIComponent(nickname)}?page=1`
                : undefined
            }
          />
        ) : !history || history.runs.length === 0 ? (
          <EmptyState>아직 저장된 Run이 없습니다.</EmptyState>
        ) : (
          <ol className={styles.runList}>
            {history.runs.map((run) => (
              <li key={run.runId}>
                <Link
                  href={`/runs/${run.runId}?fromPage=${history.page}`}
                  className={styles.runCard}
                >
                  <span
                    className={
                      run.isCleared ? styles.clearBadge : styles.deathBadge
                    }
                  >
                    {run.isCleared ? "클리어" : "사망"}
                  </span>
                  <span className={styles.runCharacter}>
                    {run.character.name}
                  </span>
                  <span>{run.reachedFloor}층 도달</span>
                  <span>{run.killCount} 처치</span>
                  <span>{formatPlayTime(run.playTime)}</span>
                  <time dateTime={run.endedAt}>{formatDate(run.endedAt)}</time>
                  <span aria-hidden="true" className={styles.arrow}>→</span>
                </Link>
              </li>
            ))}
          </ol>
        )}
        {history && history.totalPages > 1 && (
          <nav className={styles.pagination} aria-label="Run 전적 페이지">
            {history.page > 1 ? (
              <Link href={`/users/${encodeURIComponent(nickname)}?page=${history.page - 1}`}>
                ← 이전
              </Link>
            ) : (
              <span aria-disabled="true">← 이전</span>
            )}
            <strong>{history.page} / {history.totalPages} 페이지</strong>
            {history.page < history.totalPages ? (
              <Link href={`/users/${encodeURIComponent(nickname)}?page=${history.page + 1}`}>
                다음 →
              </Link>
            ) : (
              <span aria-disabled="true">다음 →</span>
            )}
          </nav>
        )}
      </section>
    </main>
  );
}

function ProgressCard({ nickname, progress, onUpdate }: {
  nickname: string;
  progress: CharacterProgressDto;
  onUpdate: (progress: CharacterProgressDto) => void;
}) {
  const [pendingSkill, setPendingSkill] = useState<SkillType | null>(null);
  const [feedback, setFeedback] = useState<{ type: "success" | "error"; message: string } | null>(null);
  const maxLevel = progress.maxLevel ?? 19;
  const maxSkillLevel = progress.maxSkillLevel ?? 10;
  const xpPercent = progress.experienceToNextLevel === 0
    ? 100
    : Math.min(100, (progress.experience / progress.experienceToNextLevel) * 100);

  const upgrade = async (skillType: SkillType) => {
    const currentLevel = skillType === "LOW_GRADE"
      ? progress.lowGradeSkillLevel
      : progress.highGradeSkillLevel;
    setPendingSkill(skillType);
    setFeedback(null);
    try {
      const updated = await upgradeCharacterSkill(
        nickname,
        progress.characterId,
        skillType,
        currentLevel + 1,
      );
      onUpdate(updated);
      const updatedLevel = skillType === "LOW_GRADE"
        ? updated.lowGradeSkillLevel
        : updated.highGradeSkillLevel;
      setFeedback({
        type: "success",
        message: updatedLevel === currentLevel || updated.skillPoints === progress.skillPoints
          ? "이미 반영된 강화 요청입니다. 현재 상태를 유지했습니다."
          : `스킬이 Lv.${updatedLevel}로 강화되었습니다.`,
      });
    } catch (error) {
      const code = error instanceof ApiClientError ? error.code : "";
      const knownMessage: Record<string, string> = {
        SKILL_POINT_NOT_ENOUGH: "사용할 수 있는 스킬 포인트가 부족합니다.",
        SKILL_LEVEL_MAX: "이미 최대 스킬 레벨입니다.",
        INVALID_SKILL_TARGET_LEVEL: "다른 요청이 먼저 반영되었습니다. 페이지를 새로 불러와 주세요.",
      };
      setFeedback({ type: "error", message: knownMessage[code] ?? getErrorMessage(error) });
    } finally {
      setPendingSkill(null);
    }
  };

  return (
    <article className={styles.progressCard}>
      <div className={styles.levelRow}>
        <div>
          <h3>{progress.characterName ?? progress.characterId}</h3>
          <p className={styles.characterId}>{progress.characterId}</p>
        </div>
        <strong>Lv.{progress.level}<small> / {maxLevel}</small></strong>
      </div>
      <div className={styles.xpHeader}>
        <span>경험치</span>
        <span>{progress.experience} / {progress.experienceToNextLevel || "MAX"}</span>
      </div>
      <div
        className={styles.xpTrack}
        role="progressbar"
        aria-label="캐릭터 경험치"
        aria-valuemin={0}
        aria-valuemax={progress.experienceToNextLevel || 1}
        aria-valuenow={progress.experienceToNextLevel === 0 ? 1 : progress.experience}
      >
        <span style={{ width: `${xpPercent}%` }} />
      </div>
      <div className={styles.pointBanner}>
        <span>사용 가능한 스킬 포인트</span>
        <strong>{progress.skillPoints}</strong>
      </div>
      <div className={styles.skills}>
        <SkillRow
          label="저학년 스킬"
          level={progress.lowGradeSkillLevel}
          maxLevel={maxSkillLevel}
          points={progress.skillPoints}
          pending={pendingSkill !== null}
          onUpgrade={() => void upgrade("LOW_GRADE")}
        />
        <SkillRow
          label="고학년 스킬"
          level={progress.highGradeSkillLevel}
          maxLevel={maxSkillLevel}
          points={progress.skillPoints}
          pending={pendingSkill !== null}
          onUpgrade={() => void upgrade("HIGH_GRADE")}
        />
      </div>
      {feedback && (
        <p className={feedback.type === "success" ? styles.success : styles.error} role="status">
          {feedback.message}
        </p>
      )}
    </article>
  );
}

function SkillRow({ label, level, maxLevel, points, pending, onUpgrade }: {
  label: string;
  level: number;
  maxLevel: number;
  points: number;
  pending: boolean;
  onUpgrade: () => void;
}) {
  const atMax = level >= maxLevel;
  const noPoints = points < 1;
  return (
    <div className={styles.skillRow}>
      <div><span>{label}</span><strong>Lv.{level} / {maxLevel}</strong></div>
      <button disabled={pending || atMax || noPoints} onClick={onUpgrade}>
        {pending ? "처리 중…" : atMax ? "최대 레벨" : noPoints ? "포인트 필요" : "강화"}
      </button>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return <div><span>{label}</span><strong>{value}</strong></div>;
}

function EmptyState({ children }: { children: React.ReactNode }) {
  return <div className={styles.empty}>{children}</div>;
}

function InlineError({ message, retry, firstPageHref }: {
  message: string;
  retry: () => void;
  firstPageHref?: string;
}) {
  return (
    <div className={styles.inlineError} role="alert">
      <p>{message}</p>
      {firstPageHref ? <Link href={firstPageHref}>첫 페이지로</Link> : <button onClick={retry}>다시 시도</button>}
    </div>
  );
}

function StatusPage({ title, message, action }: {
  title: string;
  message?: string;
  action?: React.ReactNode;
}) {
  return (
    <main className={styles.statusPage}>
      <p className={styles.eyebrow}>Trickal Run Archive</p>
      <h1>{title}</h1>
      {message && <p>{message}</p>}
      {action}
      <Link href="/search">유저 검색으로</Link>
    </main>
  );
}

function formatPlayTime(totalSeconds: number): string {
  const roundedSeconds = Math.round(totalSeconds);
  const minutes = Math.floor(roundedSeconds / 60);
  const seconds = roundedSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

function formatDate(iso: string): string {
  return new Intl.DateTimeFormat("ko-KR", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Asia/Seoul",
  }).format(new Date(iso));
}
