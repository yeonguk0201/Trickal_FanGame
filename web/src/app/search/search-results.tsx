"use client";

import Link from "next/link";
import { FormEvent, useEffect, useRef, useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import type { UserSearchResultDto } from "@/lib/meta-api-contract";
import styles from "./search.module.css";

export default function SearchResults({
  initialQuery,
  initialResults,
  initialError,
}: {
  initialQuery: string;
  initialResults: UserSearchResultDto[] | null;
  initialError: string | null;
}) {
  const router = useRouter();
  const [nickname, setNickname] = useState(initialQuery);
  const [isPending, startTransition] = useTransition();
  const resultHeading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    resultHeading.current?.focus();
  }, [initialError, initialResults]);

  const submit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const normalized = nickname.trim();
    startTransition(() => {
      router.push(`/search?q=${encodeURIComponent(normalized)}`);
    });
  };

  return (
    <main className={styles.page}>
      <header className={styles.header}>
        <Link href="/" className={styles.backLink}>← 홈으로</Link>
        <p className={styles.eyebrow}>TRICKAL RUN ARCHIVE</p>
        <h1>유저 검색</h1>
        <p>대소문자를 구분해 닉네임과 정확히 일치하는 유저를 찾습니다.</p>
      </header>

      <form onSubmit={submit} className={styles.search} aria-busy={isPending}>
        <label htmlFor="search-nickname">사용자 닉네임</label>
        <div>
          <input
            id="search-nickname"
            value={nickname}
            onChange={(event) => setNickname(event.target.value)}
            minLength={2}
            maxLength={50}
            autoComplete="off"
            autoFocus
          />
          <button type="submit" disabled={isPending}>
            {isPending ? "검색 중…" : "검색"}
          </button>
        </div>
      </form>

      <section className={styles.results} aria-live="polite" aria-busy={isPending}>
        <h2 ref={resultHeading} tabIndex={-1}>검색 결과</h2>
        {isPending ? (
          <p className={styles.status}>검색 결과를 불러오는 중입니다…</p>
        ) : initialError ? (
          <div className={styles.error} role="alert">
            <p>{initialError}</p>
            <button type="button" onClick={() => startTransition(() => router.refresh())}>다시 시도</button>
          </div>
        ) : initialResults?.length === 0 ? (
          <p className={styles.status}>일치하는 유저가 없습니다. 대소문자를 확인해 주세요.</p>
        ) : initialResults ? (
          <ul className={styles.list}>
            {initialResults.map((user) => (
              <li key={user.nickname}>
                <Link href={`/users/${encodeURIComponent(user.nickname)}`}>
                  <span>{user.nickname}</span>
                  <strong>전적 보기 →</strong>
                </Link>
              </li>
            ))}
          </ul>
        ) : null}
      </section>
    </main>
  );
}
