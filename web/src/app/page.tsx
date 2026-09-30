"use client";

import { FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import styles from "./page.module.css";

export default function Home() {
  const router = useRouter();
  const [nickname, setNickname] = useState("");

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const value = nickname.trim();
    if (value) {
      router.push(`/search?q=${encodeURIComponent(value)}`);
    }
  };

  return (
    <main className={styles.page}>
      <section className={styles.hero}>
        <p className={styles.eyebrow}>TRICKAL RUN ARCHIVE</p>
        <h1>모험의 끝에서<br />성장을 확인하세요.</h1>
        <p className={styles.description}>
          닉네임으로 최근 Run, 아티팩트 획득 순서와 캐릭터 스킬 성장을 확인할 수 있습니다.
        </p>
        <form onSubmit={submit} className={styles.search}>
          <label htmlFor="nickname">사용자 닉네임</label>
          <div>
            <input
              id="nickname"
              value={nickname}
              onChange={(event) => setNickname(event.target.value)}
              placeholder="예: test-player"
              autoComplete="off"
            />
            <button type="submit" disabled={!nickname.trim()}>검색</button>
          </div>
        </form>
        <nav className={styles.explore} aria-label="데이터 탐색">
          <Link href="/statistics">전체 통계 보기</Link>
          <Link href="/rankings">랭킹 보기</Link>
        </nav>
      </section>
      <aside className={styles.note}>
        <span>01</span><p>Run 종료 경험치와 레벨 진행</p>
        <span>02</span><p>저학년·고학년 스킬 강화</p>
        <span>03</span><p>아티팩트 획득 순서 상세</p>
      </aside>
    </main>
  );
}
