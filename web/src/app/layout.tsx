import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "트릭컬 팬게임 전적",
  description: "트릭컬 팬게임의 플레이 전적과 통계를 조회합니다.",
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="ko">
      <body>{children}</body>
    </html>
  );
}
