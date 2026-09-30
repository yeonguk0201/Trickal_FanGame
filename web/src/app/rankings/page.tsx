import RankingsDashboard from "./rankings-dashboard";
import { getErrorMessage, getRankings } from "@/lib/api-client";
import type { RankingState } from "./rankings-dashboard";

export default async function RankingsPage() {
  const [highest, fastest, clears] = await Promise.allSettled([
    getRankings("highest-floor"),
    getRankings("fastest-clear"),
    getRankings("most-clears"),
  ]);
  const initialState: RankingState = { highest: null, fastest: null, clears: null, errors: {} };
  if (highest.status === "fulfilled") initialState.highest = highest.value.data;
  else initialState.errors.highest = getErrorMessage(highest.reason);
  if (fastest.status === "fulfilled") initialState.fastest = fastest.value.data;
  else initialState.errors.fastest = getErrorMessage(fastest.reason);
  if (clears.status === "fulfilled") initialState.clears = clears.value.data;
  else initialState.errors.clears = getErrorMessage(clears.reason);
  return <RankingsDashboard initialState={initialState} />;
}
