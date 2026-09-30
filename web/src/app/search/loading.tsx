import styles from "./search.module.css";

export default function Loading() {
  return (
    <main className={styles.page} aria-busy="true">
      <header className={styles.header}>
        <p className={styles.eyebrow}>TRICKAL RUN ARCHIVE</p>
        <h1>유저 검색</h1>
      </header>
      <p className={styles.status} role="status">
        검색 결과를 불러오는 중입니다…
      </p>
    </main>
  );
}
