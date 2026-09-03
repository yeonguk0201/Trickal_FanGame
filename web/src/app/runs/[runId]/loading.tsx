import styles from "./run-detail.module.css";

export default function Loading() {
  return (
    <main className={styles.status} aria-busy="true">
      Run 상세를 불러오는 중입니다…
    </main>
  );
}
