import UserDashboard from "./user-dashboard";
import { getErrorMessage, getUserProfile, getUserRuns } from "@/lib/api-client";

export default async function UserPage({
  params,
}: PageProps<"/users/[nickname]">) {
  const { nickname } = await params;
  const [profileResult, historyResult] = await Promise.allSettled([
    getUserProfile(nickname),
    getUserRuns(nickname),
  ]);

  return (
    <UserDashboard
      nickname={nickname}
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
      initialHistoryError={
        historyResult.status === "rejected"
          ? getErrorMessage(historyResult.reason)
          : null
      }
    />
  );
}
