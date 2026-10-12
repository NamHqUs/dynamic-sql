import { useEffect } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../icon/icon";
import DataGrid from "./data-grid/data-grid";
import { fetchDbTableData, fetchDbTables } from "./database-slice";
import TableList from "./table-list/table-list";
import "./data-sources.css";

export default function DataSources() {
  const dispatch = useDispatch();
  const { selectedTable, tableStatus, dataByTable } = useSelector((state) => state.database);
  const selectedStatus = dataByTable[selectedTable]?.status;

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
      <section className="page-heading">
        <div>
          <div className="eyebrow">WORKSPACE / DATA SOURCES</div>
          <h1>Database tables</h1>
          <p>Browse tables exposed by the connected database and inspect their records.</p>
        </div>
      </section>
      <section className="database-browser">
        <TableList />
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
