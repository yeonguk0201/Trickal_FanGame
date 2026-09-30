import StatisticsDashboard from "./statistics-dashboard";
import {
  getCharacterStatistics,
  getErrorMessage,
  getFloorStatistics,
  getItemStatistics,
  getStatisticsOverview,
} from "@/lib/api-client";
import type { StatisticsState } from "./statistics-dashboard";

export default async function StatisticsPage() {
  const results = await Promise.allSettled([
    getStatisticsOverview(),
    getCharacterStatistics(),
    getItemStatistics(),
    getFloorStatistics(),
  ]);
  const keys = ["overview", "characters", "items", "floors"] as const;
  const initialState: StatisticsState = {
    overview: null,
    characters: null,
    items: null,
    floors: null,
    errors: {},
  };
  results.forEach((result, index) => {
    const key = keys[index];
    if (result.status === "fulfilled") {
      Object.assign(initialState, { [key]: result.value });
    } else {
      initialState.errors[key] = getErrorMessage(result.reason);
    }
  });
  return <StatisticsDashboard initialState={initialState} />;
}
