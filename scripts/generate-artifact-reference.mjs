import fs from "node:fs";
import path from "node:path";

const [sourcePath, outputPath, archivePath] = process.argv.slice(2);

if (!sourcePath || !outputPath || !archivePath) {
  console.error(
    "Usage: node scripts/generate-artifact-reference.mjs <source.txt> <output.md> <archive.txt>",
  );
  process.exit(1);
}

const sourceBuffer = fs.readFileSync(sourcePath);
const source = sourceBuffer.toString("utf8").replace(/^\uFEFF/, "").replace(/\r\n/g, "\n");

function cleanWiki(value, { multiline = false } = {}) {
  if (!value) return "-";

  let text = value
    .replace(/\\,/g, ",")
    .replace(/\\\|/g, "|")
    .replace(/\[br\]/gi, "\n")
    .replace(/\[anchor\([^\]]*\)\]/g, "")
    .replace(/\[\[(https?:\/\/[^|\]]+)\|([^\]]+)\]\]/g, "$2")
    .replace(/\[\[([^|\]]+)\|([^\]]+)\]\]/g, "$2")
    .replace(/\[\[([^\]]+)\]\]/g, "$1")
    .replace(/\[\*([^\]]*)\]/g, (_, note) => (note.trim() ? ` (주: ${note.trim()})` : ""))
    .replace(/\{\{\{#[^\s}]+\s+([^{}]*)\}\}\}/g, "$1")
    .replace(/\{\{\{([^{}]*)\}\}\}/g, "$1")
    .replace(/'''/g, "")
    .replace(/--([^\n]*?)--/g, "$1")
    .replace(/\[\[|\]\]/g, "")
    .replace(/\\(?=[가-힣A-Za-z0-9])/g, "")
    .replace(/\r/g, "")
    .replace(/[ \t]+\n/g, "\n")
    .replace(/\n[ \t]+/g, "\n")
    .replace(/[ \t]{2,}/g, " ")
    .replace(/,(?=[가-힣A-Za-z0-9])/g, ", ")
    .replace(/\n{3,}/g, "\n\n")
    .trim();

  if (!multiline) text = text.replace(/\n+/g, "<br>");
  return text || "-";
}

function tableCell(value) {
  return cleanWiki(value).replace(/\|/g, "\\|");
}

function headingName(raw) {
  return cleanWiki(raw.replace(/^===\s*/, "").replace(/\s*===$/, ""));
}

function parseFields(template) {
  const content = template
    .replace(/^\[include\(틀:트릭컬 리바이브\/카드 정보,\s*/, "")
    .replace(/\)\]\s*$/, "");
  const matches = [...content.matchAll(/(?:^|(?<!\\),)\s*([가-힣A-Za-z0-9_]+)=/gm)];
  const fields = {};

  for (let i = 0; i < matches.length; i += 1) {
    const key = matches[i][1];
    const start = matches[i].index + matches[i][0].length;
    const end = i + 1 < matches.length ? matches[i + 1].index : content.length;
    fields[key] = content.slice(start, end).trim();
  }

  return fields;
}

function composeNumbered(fields, prefix = "", max = 20) {
  const parts = [];
  for (let index = 1; index <= max; index += 1) {
    const description = fields[`${prefix}설명${index}`];
    const number = fields[`${prefix}수치${index}`];
    const unit = fields[`${prefix}단위${index}`];
    if (description !== undefined) parts.push(description);
    if (number !== undefined) parts.push(number);
    if (unit !== undefined) parts.push(unit);
    if (fields[`${prefix}줄바꿈${index}`] === "1") parts.push("\n");
  }
  let text = "";
  for (const part of parts) {
    if (part === "\n") {
      text += "\n";
      continue;
    }
    const fragment = cleanWiki(part, { multiline: true });
    if (fragment === "-") continue;
    const left = text.at(-1) ?? "";
    const shouldAddSpace =
      /[가-힣A-Za-z0-9)]/.test(left) &&
      /^[가-힣A-Za-z0-9]/.test(fragment) &&
      !(/\d/.test(left) && /^(초|회|개|명|점|단계|배|칸)/.test(fragment)) &&
      !/^(한다|된다|시킨다|이다|이며|이고|인|을|를|이|가|은|는|과|와|로|으로|에|의)/.test(
        fragment,
      );
    if (shouldAddSpace) text += " ";
    text += fragment;
  }
  return cleanWiki(text);
}

function listStats(fields, level = 1) {
  const values = [];
  for (let index = 1; index <= 10; index += 1) {
    const name = fields[`스탯${index}`];
    const value = fields[`스탯${index}_Lv${level}`];
    if (name && value) values.push(`${cleanWiki(name)} ${cleanWiki(value)}`);
  }
  for (let index = 1; index <= 10; index += 1) {
    const name = fields[`수치명${index}`];
    const value = fields[`수치${index}_Lv${level}`];
    if (name && value) values.push(`${cleanWiki(name)} ${cleanWiki(value)}`);
  }
  return values.join("<br>") || "-";
}

function attachmentSummary(fields) {
  if (fields["애착"] !== "1") return "-";
  const values = [];
  if (fields["애착_사도명"]) values.push(`대상: ${cleanWiki(fields["애착_사도명"])}`);
  if (fields["애착_스킬명"]) values.push(`스킬: ${cleanWiki(fields["애착_스킬명"])}`);
  let description = "";
  let lastWasState = false;
  const appendDescription = (raw, isState = false) => {
    const fragment = cleanWiki(raw, { multiline: true });
    if (fragment === "-") return;
    if (description && !description.endsWith("\n")) {
      const startsWithParticle = /^(을|를|이|가|과|와|로|으로|에|의|은|는)/.test(fragment);
      if (isState || !lastWasState || !startsWithParticle) description += " ";
    }
    description += fragment;
    lastWasState = isState;
  };
  for (let index = 1; index <= 20; index += 1) {
    if (fields[`애착_설명${index}`]) {
      appendDescription(fields[`애착_설명${index}`]);
    }
    if (fields[`애착_상태${index}`]) {
      appendDescription(fields[`애착_상태${index}`], true);
    }
    if (fields[`애착_줄바꿈${index}`] === "1") {
      description += "\n";
    }
  }
  if (description) values.push(cleanWiki(description));
  if (fields["애착_상태설명1"]) values.push(cleanWiki(fields["애착_상태설명1"]));
  for (let index = 1; index <= 20; index += 1) {
    const name = fields[`애착_수치명${index}`];
    const number = fields[`애착_수치${index}`];
    const unit = fields[`애착_단위${index}`] ?? "";
    const prefix = fields[`애착_수치한글${index}`]
      ? `${cleanWiki(fields[`애착_수치한글${index}`])} `
      : "";
    if (name && number) {
      values.push(
        `${cleanWiki(name)}: ${prefix}${cleanWiki(number)}${cleanWiki(unit) === "-" ? "" : cleanWiki(unit)}`,
      );
    }
  }
  const level3 = composeNumbered(
    Object.fromEntries(
      Object.entries(fields)
        .filter(([key]) => key.startsWith("애착_3레벨"))
        .map(([key, value]) => [key.replace("애착_3레벨", "애착_"), value]),
    ),
    "애착_",
  );
  if (level3 !== "-") values.push(`3레벨: ${level3}`);
  if (fields["애착_기준"]) values.push(cleanWiki(fields["애착_기준"]));
  return values.join("<br>") || "-";
}

function parseLegacy(block) {
  const cost = block.match(/구매 비용\}\}\}\s*'''([^']+)'''/)?.[1]?.trim() ?? "-";
  const rows = [...block.matchAll(/^\|\|(.+?)\|\|\s*$/gm)].map((match) => match[1]);
  const usefulRows = rows.filter(
    (row) =>
      !row.includes("tablewidth") &&
      !row.includes("<|4>") &&
      !row.includes("등급}}}") &&
      !row.includes("파일:트릭컬_"),
  );
  const statsRow = usefulRows.find((row) => row.includes("Icon_"));
  const statsIndex = statsRow ? usefulRows.indexOf(statsRow) : -1;
  const effectRow = statsIndex >= 0 ? usefulRows[statsIndex + 1] : undefined;
  const flavorRow = statsIndex >= 0 ? usefulRows[statsIndex + 2] : undefined;

  const cleanRow = (row) => {
    if (!row) return "-";
    return cleanWiki(
      row
        .replace(/<[^>]+>/g, "")
        .replace(/\[\[파일:Icon_[^\]]+\]\]/g, ""),
    );
  };

  const tableEnd = block.lastIndexOf("}}}");
  const commentary = tableEnd >= 0 ? block.slice(tableEnd + 3).trim() : "";
  return {
    cost,
    stats: cleanRow(statsRow),
    effect: cleanRow(effectRow),
    flavor: cleanRow(flavorRow),
    commentary: cleanWiki(commentary),
    levels: [],
    attachment: "-",
  };
}

function parseTemplate(block) {
  const start = block.indexOf("[include(틀:트릭컬 리바이브/카드 정보,");
  const end = block.indexOf(")]", start);
  const template = block.slice(start, end + 2);
  const fields = parseFields(template);
  const commentary = block.slice(end + 2).trim();
  const levels = [];

  for (let level = 1; level <= 30; level += 1) {
    const stats = listStats(fields, level);
    if (stats !== "-") levels.push({ level, stats });
  }

  return {
    cost: cleanWiki(fields["비용"]),
    stats: listStats(fields, 1),
    effect: composeNumbered(fields),
    flavor: cleanWiki(fields["카드설명"]),
    commentary: cleanWiki(commentary),
    levels,
    attachment: attachmentSummary(fields),
  };
}

const lines = source.split("\n");
const items = [];
let rarity = "미분류";
let globalOnly = false;

for (let index = 0; index < lines.length; index += 1) {
  const line = lines[index];
  const rarityMatch = line.match(/^==(?!=)\s*(.+?)\s*==$/);
  if (rarityMatch) {
    const label = cleanWiki(rarityMatch[1]);
    globalOnly = label.includes("글로벌 서버 전용");
    if (label.includes("전설")) rarity = globalOnly ? "전설 · 글로벌 서버 전용" : "전설";
    else if (label.includes("희귀")) rarity = "희귀";
    else if (label.includes("고급")) rarity = "고급";
    else if (label.includes("일반")) rarity = "일반";
    continue;
  }

  if (!/^===\s*.+?\s*===$/.test(line)) continue;
  let end = index + 1;
  while (end < lines.length && !/^={2,3}\s*.+?\s*={2,3}$/.test(lines[end])) end += 1;
  const block = lines.slice(index + 1, end).join("\n").trim();
  const parsed = block.includes("[include(틀:트릭컬 리바이브/카드 정보,")
    ? parseTemplate(block)
    : parseLegacy(block);

  items.push({
    name: headingName(line),
    rarity,
    globalOnly,
    ...parsed,
  });
  index = end - 1;
}

const rarityOrder = ["전설", "전설 · 글로벌 서버 전용", "희귀", "고급", "일반"];
const now = new Date();
const generatedDate = new Intl.DateTimeFormat("ko-KR", {
  timeZone: "Asia/Seoul",
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
}).format(now);

const out = [];
out.push("# 트릭컬 리바이브 아티팩트 원본 참고표");
out.push("");
out.push("> 이 문서는 팬게임 아이템 설계 시 원작 효과와 조합 아이디어를 비교하기 위한 참고 자료다.");
out.push("> 원작 문구는 밸런스 확정안이 아니며, 팬게임 구현 단계에서 효과·수치·시너지를 별도로 조정한다.");
out.push("");
out.push("## 문서 사용 기준");
out.push("");
out.push(`- 수록 범위: 아티팩트 ${items.length}개`);
out.push(`- 생성일: ${generatedDate} (Asia/Seoul)`);
out.push("- `원본 효과`는 나무위키 카드 정보 틀의 설명/수치 필드를 순서대로 합친 문구다.");
out.push("- `Lv.1 능력치`와 `레벨별 수치`는 원문에 값이 있는 항목만 표시한다.");
out.push("- `애착 효과`와 `원문 해설`은 시너지 후보를 찾기 위한 참고 정보이며 팬게임 설계안이 아니다.");
out.push("- 원문에 제목이나 짧은 메모만 있는 항목은 확인 가능한 필드만 기록하고 나머지를 `-`로 표시한다.");
out.push("- 나무위키 매크로를 제거하는 과정에서 표시가 단순화될 수 있으므로 정확한 원문은 [원본 보관 파일](references/trickcal-revive-artifacts-namuwiki-source.txt)에서 대조한다.");
out.push("");
out.push("## 빠른 분류");
out.push("");
out.push("| 등급 | 개수 |");
out.push("|---|---:|");
for (const group of rarityOrder) {
  out.push(`| ${group} | ${items.filter((item) => item.rarity === group).length} |`);
}

for (const group of rarityOrder) {
  const groupItems = items.filter((item) => item.rarity === group);
  if (groupItems.length === 0) continue;
  out.push("");
  out.push(`## ${group}`);
  out.push("");
  out.push("| 아이템 | 비용 | Lv.1 능력치 | 원본 효과 | 애착 효과 | 카드 설명 | 원문 해설·활용 참고 |");
  out.push("|---|---:|---|---|---|---|---|");
  for (const item of groupItems) {
    out.push(
      `| ${tableCell(item.name)} | ${tableCell(item.cost)} | ${tableCell(item.stats)} | ${tableCell(item.effect)} | ${tableCell(item.attachment)} | ${tableCell(item.flavor)} | ${tableCell(item.commentary)} |`,
    );
  }
}

const scalableItems = items.filter((item) => item.levels.length > 1);
out.push("");
out.push("## 레벨별 수치");
out.push("");
out.push("원문에 2레벨 이상의 값이 기재된 아이템만 수록한다. 빈 값은 원문에도 값이 없다는 뜻이다.");

for (const item of scalableItems) {
  out.push("");
  out.push("<details>");
  out.push(`<summary>${item.rarity} · ${item.name}</summary>`);
  out.push("");
  out.push("| 레벨 | 능력치·효과 수치 |");
  out.push("|---:|---|");
  for (const row of item.levels) {
    out.push(`| ${row.level} | ${tableCell(row.stats)} |`);
  }
  out.push("");
  out.push("</details>");
}

out.push("");
out.push("## 갱신 방법");
out.push("");
out.push("새 원본 TXT를 받으면 아래 명령으로 참고표와 원본 보관 파일을 함께 갱신한다.");
out.push("");
out.push("```powershell");
out.push("node scripts/generate-artifact-reference.mjs <원본-TXT> docs/11-artifact-reference.md docs/references/trickcal-revive-artifacts-namuwiki-source.txt");
out.push("```");
out.push("");

fs.mkdirSync(path.dirname(outputPath), { recursive: true });
fs.mkdirSync(path.dirname(archivePath), { recursive: true });
fs.writeFileSync(outputPath, `${out.join("\n")}\n`, "utf8");
fs.writeFileSync(archivePath, sourceBuffer);

console.log(`Parsed ${items.length} items.`);
console.log(`Wrote ${outputPath}`);
console.log(`Archived ${archivePath}`);
