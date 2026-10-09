export function parseCsv(text) {
  const lines = text.trim().split(/\r?\n/).filter(Boolean);
  if (lines.length < 2) return [];
  const headers = lines[0].split(",").map((header) => header.trim());
  return lines.slice(1).map((line, index) => {
    const values = line.split(",").map((value) => value.trim());
    const parsed = Object.fromEntries(headers.map((header, i) => [header, values[i] === undefined || values[i] === "" ? "" : isNaN(Number(values[i])) ? values[i] : Number(values[i])]));
    return { id: index + 1, ...parsed };
  });
}

export function toCsv(rows) {
  if (!rows.length) return "";
  const headers = Object.keys(rows[0]);
  return [headers.join(","), ...rows.map((row) => headers.map((header) => String(row[header] ?? "").replace(/,/g, " ")).join(","))].join("\n");
}
