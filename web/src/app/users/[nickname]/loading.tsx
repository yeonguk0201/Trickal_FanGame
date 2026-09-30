import styles from "./user-dashboard.module.css";

export default function Loading() {
  return (
    <main className={styles.statusPage} aria-busy="true">
      <p className={styles.eyebrow}>TRICKAL RUN ARCHIVE</p>
      <h1>사용자 기록을 불러오는 중입니다…</h1>
    </main>
  );
}
