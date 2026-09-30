import styles from "./statistics.module.css";

export default function Loading() {
  return (
    <main className={styles.statusPage} aria-live="polite">
      <p className={styles.eyebrow}>DATA ARCHIVE</p>
      <h1>통계를 불러오는 중입니다…</h1>
    </main>
  );
}
