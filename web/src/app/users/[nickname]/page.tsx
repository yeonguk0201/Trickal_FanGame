interface Character {
  id: string;
  name: string;
}

interface Run {
  runId: string;
  character: Character;
  reachedFloor: number;
  playTime: number;
  isCleared: boolean;
  killCount: number;
  endedAt: string;
}

interface RunsResponse {
  success: boolean;
  data: Run[];
  meta: { page: number; limit: number; total: number };
  code?: string;
  message?: string;
}

interface ErrorResponse {
  success: false;
  error?: {
    code?: string;
    message?: string;
  };
}

async function getUserRuns(nickname: string): Promise<RunsResponse | null> {
  const apiUrl = process.env.NEXT_PUBLIC_API_URL || "http://localhost:3001/api";

  try {
    const res = await fetch(`${apiUrl}/users/${encodeURIComponent(nickname)}/runs`, {
      cache: "no-store",
    });

    if (!res.ok) {
      if (res.status === 404) {
        return null;
      }
      const error: ErrorResponse = await res.json();
      throw new Error(error.error?.message || "Failed to fetch runs");
    }

    return res.json();
  } catch {
    return null;
  }
}

function formatPlayTime(totalSeconds: number): string {
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
}

function formatDate(iso: string): string {
  const koreaOffsetMs = 9 * 60 * 60 * 1000;
  const date = new Date(new Date(iso).getTime() + koreaOffsetMs);
  const twoDigits = (value: number) => value.toString().padStart(2, "0");

  return `${date.getUTCFullYear()}.${twoDigits(date.getUTCMonth() + 1)}.${twoDigits(
    date.getUTCDate(),
  )}. ${twoDigits(date.getUTCHours())}:${twoDigits(date.getUTCMinutes())}`;
}

export default async function UserRunsPage({
  params,
}: {
  params: Promise<{ nickname: string }>;
}) {
  const { nickname } = await params;
  const result = await getUserRuns(nickname);

  if (!result) {
    return (
      <main style={styles.main}>
        <h1 style={styles.title}>{nickname}</h1>
        <p style={styles.error}>유저를 찾을 수 없거나 데이터를 불러오지 못했습니다.</p>
      </main>
    );
  }

  const { data: runs, meta } = result;

  return (
    <main style={styles.main}>
      <h1 style={styles.title}>{nickname}의 전적</h1>
      <p style={styles.subtitle}>총 {meta.total}회 플레이</p>

      {runs.length === 0 ? (
        <p style={styles.empty}>아직 플레이 기록이 없습니다.</p>
      ) : (
        <ul style={styles.list}>
          {runs.map((run) => (
            <li key={run.runId} style={styles.item}>
              <div style={styles.row}>
                <span style={run.isCleared ? styles.cleared : styles.death}>
                  {run.isCleared ? "클리어" : "사망"}
                </span>
                <span style={styles.floor}>{run.reachedFloor}층</span>
                <span style={styles.kills}>{run.killCount}킬</span>
                <span style={styles.time}>{formatPlayTime(run.playTime)}</span>
              </div>
              <div style={styles.meta}>
                <span>{run.character.name}</span>
                <span>{formatDate(run.endedAt)}</span>
              </div>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}

const styles: Record<string, React.CSSProperties> = {
  main: {
    maxWidth: 600,
    margin: "0 auto",
    padding: "2rem 1rem",
    fontFamily: "system-ui, sans-serif",
  },
  title: {
    fontSize: "1.5rem",
    fontWeight: 600,
    marginBottom: "0.25rem",
  },
  subtitle: {
    color: "#666",
    marginBottom: "1.5rem",
  },
  error: {
    color: "#dc2626",
    padding: "1rem",
    background: "#fef2f2",
    borderRadius: 8,
  },
  empty: {
    color: "#666",
    textAlign: "center",
    padding: "2rem",
  },
  list: {
    listStyle: "none",
    padding: 0,
    margin: 0,
    display: "flex",
    flexDirection: "column",
    gap: "0.75rem",
  },
  item: {
    padding: "0.75rem 1rem",
    background: "#f9fafb",
    borderRadius: 8,
    border: "1px solid #e5e7eb",
  },
  row: {
    display: "flex",
    gap: "0.75rem",
    alignItems: "center",
    marginBottom: "0.25rem",
  },
  cleared: {
    color: "#059669",
    fontWeight: 600,
  },
  death: {
    color: "#dc2626",
    fontWeight: 600,
  },
  floor: {
    fontWeight: 500,
  },
  kills: {
    color: "#666",
  },
  time: {
    marginLeft: "auto",
    color: "#666",
    fontFamily: "monospace",
  },
  meta: {
    fontSize: "0.875rem",
    color: "#999",
    display: "flex",
    justifyContent: "space-between",
  },
};
