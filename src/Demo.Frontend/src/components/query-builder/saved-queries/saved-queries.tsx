import { useMemo } from "react";
import Icon from "../../icon/icon";
import { StoredQueryRequest } from "../query-design/query-builder-slice";
import "./saved-queries.css";

type SavedQueriesProps = {
  requests: StoredQueryRequest[];
  status: "idle" | "loading" | "succeeded" | "failed";
  error: string;
  selectedFileName: string;
  disabled: boolean;
  onSelectRequest: (fileName: string) => void;
};

export default function SavedQueries({
  requests,
  status,
  error,
  selectedFileName,
  disabled,
  onSelectRequest,
}: SavedQueriesProps) {
  const sortedRequests = useMemo(() => [...requests].sort((left, right) => {
    const leftName = left.query.name || left.fileName.replace(/\.json$/i, "");
    const rightName = right.query.name || right.fileName.replace(/\.json$/i, "");
    return leftName.localeCompare(rightName, undefined, { numeric: true, sensitivity: "base" })
      || left.fileName.localeCompare(right.fileName, undefined, { numeric: true, sensitivity: "base" });
  }), [requests]);

  return (
    <aside className="query-request-list split-panel">
      <div className="metadata-list-head">
        <span className="section-kicker">SAVED QUERY REQUESTS</span>
        <span className="tab-number">{requests.length}</span>
      </div>
      {status === "loading" && <p className="query-message">Loading requests…</p>}
      {status === "failed" && <p className="query-message error" role="alert">{error}</p>}
      {status !== "loading" && sortedRequests.map((saved) => (
        <button
          className={`query-request-item${saved.fileName === selectedFileName ? " active" : ""}`}
          key={saved.fileName}
          type="button"
          onClick={() => onSelectRequest(saved.fileName)}
          disabled={disabled}
        >
          <strong>{saved.query.name || saved.fileName.replace(/\.json$/i, "")}</strong>
          <small>{saved.query.entity} · {saved.query.fields.length} columns</small>
          <Icon name="chevron" size={14} />
        </button>
      ))}
      {status !== "loading" && !requests.length && <p className="query-message">No saved requests.</p>}
    </aside>
  );
}
