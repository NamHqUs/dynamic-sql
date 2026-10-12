import { useDispatch, useSelector } from "react-redux";
import Icon from "../../icon/icon";
import { selectDbTable } from "../database-slice";
import "./table-list.css";

export default function TableList() {
  const dispatch = useDispatch();
  const { tables, selectedTable, tableStatus, tableError } = useSelector((state) => state.database);

  return (
    <aside className="database-sidebar" aria-label="Database tables">
      <div className="database-sidebar-heading">
        <span className="section-kicker">TABLES</span>
        {tableStatus === "succeeded" && <span className="database-table-count">{tables.length}</span>}
      </div>
      {tableStatus === "loading" && <p className="database-message">Loading tables…</p>}
      {tableStatus === "failed" && <p className="database-message error" role="alert">{tableError}</p>}
      {tableStatus === "succeeded" && tables.map((table) => (
        <button
          className={`database-table-item${table === selectedTable ? " active" : ""}`}
          key={table}
          onClick={() => dispatch(selectDbTable(table))}
          aria-current={table === selectedTable ? "page" : undefined}
        >
          <Icon name="database" size={15} />
          <span>{table}</span>
          <Icon name="chevron" size={14} />
        </button>
      ))}
      {tableStatus === "succeeded" && tables.length === 0 && <p className="database-message">No tables are available.</p>}
    </aside>
  );
}
