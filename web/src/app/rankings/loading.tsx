import styles from "../statistics/statistics.module.css";

export default function Loading() {
  return <main className={styles.statusPage} aria-live="polite"><p className={styles.eyebrow}>HALL OF FAME</p><h1>랭킹을 불러오는 중입니다…</h1></main>;
}
