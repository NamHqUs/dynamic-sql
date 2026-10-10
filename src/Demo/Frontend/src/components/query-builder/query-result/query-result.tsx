import { QueryResult as QueryResultData } from "../query-design/query-builder-slice";
import Icon from "../../icon/icon";
import "./query-result.css";

type QueryResultProps = {
  result: QueryResultData | null;
  onRunQuery: () => void;
  queryLoading: boolean;
  queryDisabled: boolean;
};

export default function QueryResult({ result, onRunQuery, queryLoading, queryDisabled }: QueryResultProps) {
  return (
    <section className="results-card query-results">
      <div className="results-head">
        <div className="query-result-title">
          <div><span className="section-kicker">QUERY RESULT</span><h2>{result?.name || "Run a query to see results"}</h2></div>
          {result && <span className="result-count">{result.rows.length} records</span>}
        </div>
        <div className="query-result-tools">
          <button className="button primary" onClick={onRunQuery} disabled={queryLoading || queryDisabled}>
            <Icon name="play" size={14} /> {queryLoading ? "Running…" : "Run query"}
          </button>
        </div>
      </div>
      {result ? <div className="table-wrap"><table><thead><tr>{result.columns.map((column, index) => <th key={`${column}-${index}`}>{column}</th>)}</tr></thead><tbody>{result.rows.map((row, rowIndex) => <tr key={rowIndex}>{result.columns.map((_, columnIndex) => <td key={columnIndex}>{row[columnIndex] ?? ""}</td>)}</tr>)}</tbody></table>{!result.rows.length && <p className="empty-state">No records matched this query.</p>}</div> : <p className="empty-state">Your query results will appear here.</p>}
    </section>
  );
}
