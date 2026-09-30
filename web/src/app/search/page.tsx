import SearchResults from "./search-results";
import { getErrorMessage, searchUsers } from "@/lib/api-client";

export default async function SearchPage({
  searchParams,
}: PageProps<"/search">) {
  const query = await searchParams;
  const rawQuery = Array.isArray(query.q) ? query.q[0] : query.q;
  const normalizedQuery = (rawQuery ?? "").trim();
  let results = null;
  let error = null;

  if (normalizedQuery.length < 2 || normalizedQuery.length > 50) {
    error = "닉네임은 앞뒤 공백을 제외하고 2~50자로 입력해 주세요.";
  } else {
    try {
      results = await searchUsers(normalizedQuery);
    } catch (searchError) {
      error = getErrorMessage(searchError);
    }
  }

  return (
    <SearchResults
      key={normalizedQuery}
      initialQuery={normalizedQuery}
      initialResults={results}
      initialError={error}
    />
  );
}
