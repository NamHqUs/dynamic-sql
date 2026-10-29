import { useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import Icon from "../../icon/icon";
import DataGrid from "../data-grid/data-grid";
import { selectDbTable } from "../database-slice";
import "./table-details.css";

export default function TableDetails() {
  const dispatch = useDispatch();
  const { schema, selectedTable, selectedSchema, dataByTable } = useSelector((state) => state.database);
  const [activeTab, setActiveTab] = useState<"columns" | "relations" | "data">("columns");
  const table = schema.tables.find((item) => item.name === selectedTable && item.schema === selectedSchema);
  if (!table) return null;
  const tableDataKey = selectedSchema ? `${selectedSchema}.${selectedTable}` : selectedTable;
  const tableData = dataByTable[tableDataKey];

  const relations = schema.relationships.flatMap((relation) => {
    if (relation.sourceTable === table.name && relation.sourceSchema === table.schema) {
      return [{ relation, otherSchema: relation.targetSchema, otherTable: relation.targetTable }];
    }
    if (relation.targetTable === table.name && relation.targetSchema === table.schema) {
      return [{ relation, otherSchema: relation.sourceSchema, otherTable: relation.sourceTable }];
    }
    return [];
  });

  return (
    <section className="table-details" aria-label={`${table.name} table details`}>
      <div className="table-details-heading">
        <div className="table-details-title">
          <span className="section-kicker">TABLE DETAILS</span>
          <h3><span>{table.schema || "Default schema"}</span><Icon name="chevron" size={13} />{table.name}</h3>
        </div>
        <div className="table-details-summary">
          <span>{table.columns.length} columns</span>
          {tableData?.status === "succeeded" && <span>{tableData.rows.length} preview rows</span>}
        </div>
      </div>

      <section className="table-details-columns-section" aria-label="Table details">
        <div className="table-details-tabs" role="tablist" aria-label="Table details">
          <button
            type="button"
            id="table-details-columns-tab"
            role="tab"
            aria-selected={activeTab === "columns"}
            aria-controls="table-details-columns-panel"
            className={activeTab === "columns" ? "active" : ""}
            onClick={() => setActiveTab("columns")}
          >
            COLUMNS
          </button>
          <button
            type="button"
            id="table-details-relations-tab"
            role="tab"
            aria-selected={activeTab === "relations"}
            aria-controls="table-details-relations-panel"
            className={activeTab === "relations" ? "active" : ""}
            onClick={() => setActiveTab("relations")}
          >
            RELATIONS
          </button>
          <button
            type="button"
            id="table-details-data-tab"
            role="tab"
            aria-selected={activeTab === "data"}
            aria-controls="table-details-data-panel"
            className={activeTab === "data" ? "active" : ""}
            onClick={() => setActiveTab("data")}
          >
            DATA
          </button>
        </div>
        {activeTab === "columns" ? (
          <div
            className="table-details-tab-panel"
            id="table-details-columns-panel"
            role="tabpanel"
            aria-labelledby="table-details-columns-tab"
            tabIndex={0}
          >
            <div className="table-details-column-head">
              <span>Column</span><span>Data type</span><span>Key</span>
            </div>
            {table.columns.map((column) => (
              <div className="table-details-column" key={column.name}>
                <span className="table-details-column-name">{column.name}</span>
                <span className="table-details-column-type">{column.dataType || "—"}</span>
                <span>{column.isPrimaryKey ? <span className="table-details-key">PRIMARY</span> : "—"}</span>
              </div>
            ))}
            {table.columns.length === 0 && <p className="database-message">No column metadata is available.</p>}
          </div>
        ) : activeTab === "relations" ? (
          <div
            className="table-details-tab-panel"
            id="table-details-relations-panel"
            role="tabpanel"
            aria-labelledby="table-details-relations-tab"
            tabIndex={0}
          >
            {relations.length > 0 ? relations.map((relation) => (
              <div className="table-details-relation" key={`${relation.relation.name}-${relation.otherSchema}-${relation.otherTable}`}>
                <div className="table-details-joined-keys">
                  <span>{relation.relation.sourceColumns.join(", ")}</span>
                  <Icon name="chevron" size={13} />
                  <span>{relation.relation.targetColumns.join(", ")}</span>
                </div>
                <button
                  type="button"
                  onClick={() => dispatch(selectDbTable({ name: relation.otherTable, schema: relation.otherSchema }))}
                  title={`View ${relation.otherSchema ? `${relation.otherSchema}.` : ""}${relation.otherTable}`}
                >
                  <Icon name="table" size={13} />
                  {relation.otherSchema ? `${relation.otherSchema}.` : ""}{relation.otherTable}
                  <Icon name="chevron" size={12} />
                </button>
              </div>
            )) : <p className="database-message">No table relations.</p>}
          </div>
        ) : (
          <div
            className="table-details-tab-panel table-details-data-panel"
            id="table-details-data-panel"
            role="tabpanel"
            aria-labelledby="table-details-data-tab"
            tabIndex={0}
          >
            <p className="table-details-data-note">Previewing up to 10 rows.</p>
            <DataGrid showHeading={false} />
          </div>
        )}
      </section>
    </section>
  );
}
