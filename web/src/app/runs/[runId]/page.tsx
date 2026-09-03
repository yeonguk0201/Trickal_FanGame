import RunDetail from "./run-detail";
import { ApiClientError, getErrorMessage, getRunDetail } from "@/lib/api-client";
import type { RunDetailDto } from "@/lib/meta-api-contract";

export default async function RunDetailPage({
  params,
  searchParams,
}: PageProps<"/runs/[runId]">) {
  const { runId } = await params;
  const query = await searchParams;
  const rawFromPage = Array.isArray(query.fromPage)
    ? query.fromPage[0]
    : query.fromPage;
  const returnPage = rawFromPage && /^[1-9]\d*$/.test(rawFromPage)
    ? rawFromPage
    : null;
  let initialRun: RunDetailDto | null = null;
  let initialError: string | null = null;
  let initialErrorCode: string | null = null;
  try {
    initialRun = await getRunDetail(runId);
  } catch (error) {
    initialError = getErrorMessage(error);
    initialErrorCode = error instanceof ApiClientError ? error.code : null;
  }

  return (
    <RunDetail
      runId={runId}
      returnPage={returnPage}
      initialRun={initialRun}
      initialError={initialError}
      initialErrorCode={initialErrorCode}
    />
  );
}
