import UserDashboard from "./user-dashboard";
import {
  ApiClientError,
  getErrorMessage,
  getUserProfile,
  getUserRuns,
} from "@/lib/api-client";

const RUNS_PER_PAGE = 10;

export default async function UserPage({
  params,
  searchParams,
}: PageProps<"/users/[nickname]">) {
  const { nickname } = await params;
  const query = await searchParams;
  const requestedPage = Array.isArray(query.page)
    ? query.page[0] ?? "1"
    : query.page ?? "1";
  const [profileResult, historyResult] = await Promise.allSettled([
    getUserProfile(nickname),
    getUserRuns(nickname, requestedPage, RUNS_PER_PAGE),
  ]);

  return (
    <UserDashboard
      key={`${nickname}-${requestedPage}`}
      nickname={nickname}
      requestedPage={requestedPage}
      initialUser={
        profileResult.status === "fulfilled" ? profileResult.value : null
      }
      initialHistory={
        historyResult.status === "fulfilled" ? historyResult.value : null
      }
      initialProfileError={
        profileResult.status === "rejected"
          ? getErrorMessage(profileResult.reason)
          : null
      }
      initialProfileErrorCode={
        profileResult.status === "rejected" &&
        profileResult.reason instanceof ApiClientError
          ? profileResult.reason.code
          : null
      }
      initialHistoryError={
        historyResult.status === "rejected"
          ? getErrorMessage(historyResult.reason)
          : null
      }
      initialHistoryErrorCode={
        historyResult.status === "rejected" &&
        historyResult.reason instanceof ApiClientError
          ? historyResult.reason.code
          : null
      }
    />
  );
}
