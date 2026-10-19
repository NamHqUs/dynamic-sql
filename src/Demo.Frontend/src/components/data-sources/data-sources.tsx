import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../icon/icon";
import PanelSplitter from "../panel-splitter/panel-splitter";
import { useResizablePanel } from "../../utils/use-resizable-panel";
import DataGrid from "./data-grid/data-grid";
import { fetchDbTableData, fetchDbTables } from "./database-slice";
import TableList from "./table-list/table-list";
import "./data-sources.css";

export default function DataSources() {
  const dispatch = useDispatch();
  const { tables, selectedTable, tableStatus, dataByTable } = useSelector((state) => state.database);
  const selectedStatus = dataByTable[selectedTable]?.status;
  const selectedData = dataByTable[selectedTable];
  const panel = useResizablePanel("data-sources");

  useEffect(() => {
    dispatch(fetchDbTables());
  }, [dispatch]);

  useEffect(() => {
    if (selectedTable && selectedStatus !== "succeeded" && selectedStatus !== "loading") {
      dispatch(fetchDbTableData(selectedTable));
    }
  }, [dispatch, selectedTable, selectedStatus]);

  return (
    <>
      <section className="page-heading database-page-heading">
        <div className="database-heading-main">
          <span className="database-heading-mark" aria-hidden="true"><Icon name="database" size={21} /></span>
          <div className="database-heading-copy">
            <span className="database-heading-eyebrow">DATA EXPLORER <i /> TABLES · COLUMNS · RECORDS</span>
            <h2>Explore your database</h2>
            <p>Browse available tables, inspect their columns, and explore your records.</p>
            <div className="database-heading-summary" aria-label="Database summary">
              <span className="database-heading-current"><Icon name="database" size={13} />{selectedTable || "No table selected"}</span>
              <span><strong>{tableStatus === "succeeded" ? tables.length : "—"}</strong> tables</span>
              <span><strong>{selectedStatus === "succeeded" && selectedData ? selectedData.columns.length : "—"}</strong> columns</span>
              <span><strong>{selectedStatus === "succeeded" && selectedData ? selectedData.rows.length : "—"}</strong> records</span>
            </div>
          </div>
        </div>
      </section>
      <section className={`database-browser split-workspace${panel.collapsed ? " panel-collapsed" : ""}`} style={panel.style}>
        <TableList />
        <PanelSplitter panel={panel} label="tables panel" />
        <section className="database-content" aria-live="polite">
          {selectedTable ? <DataGrid /> : (
            <div className="database-placeholder">
              <Icon name="database" size={24} />
              <p>{tableStatus === "succeeded" ? "Select a table to view its records." : "Loading database tables…"}</p>
            </div>
          )}
        </section>
      </section>
    </>
  );
}
