import RunDetail from "./run-detail";
import { getErrorMessage, getRunDetail } from "@/lib/api-client";
import type { RunDetailDto } from "@/lib/meta-api-contract";

export default async function RunDetailPage({
  params,
}: PageProps<"/runs/[runId]">) {
  const { runId } = await params;
  let initialRun: RunDetailDto | null = null;
  let initialError: string | null = null;
  try {
    initialRun = await getRunDetail(runId);
  } catch (error) {
    initialError = getErrorMessage(error);
  }

  return (
    <RunDetail
      runId={runId}
      initialRun={initialRun}
      initialError={initialError}
    />
  );
}
