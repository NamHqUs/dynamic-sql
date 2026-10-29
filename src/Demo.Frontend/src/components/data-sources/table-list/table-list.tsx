import { useDispatch, useSelector } from "react-redux";
import Icon from "../../icon/icon";
import { selectDbTable } from "../database-slice";
import "./table-list.css";

export default function TableList() {
  const dispatch = useDispatch();
  const { schema, selectedTable, selectedSchema, tableStatus, tableError, schemaStatus, schemaError } = useSelector((state) => state.database);
  const tablesBySchema = new Map();
  for (const table of schema.tables) {
    const schemaName = table.schema || "";
    const group = tablesBySchema.get(schemaName) || [];
    group.push(table);
    tablesBySchema.set(schemaName, group);
  }
  const schemaNames = Array.from(tablesBySchema.keys()).sort((left, right) => left.localeCompare(right));

  return (
    <aside className="database-sidebar split-panel" aria-label="Database tables">
      <div className="database-sidebar-heading">
        <span className="section-kicker">TABLES</span>
        {schemaStatus === "succeeded" && <span className="database-table-count">{schema.tables.length}</span>}
      </div>
      {(tableStatus === "loading" || schemaStatus === "loading") && <p className="database-message">Loading tables…</p>}
      {tableStatus === "failed" && <p className="database-message error" role="alert">{tableError}</p>}
      {schemaStatus === "failed" && <p className="database-message error" role="alert">{schemaError}</p>}
      {schemaStatus === "succeeded" && schemaNames.map((schemaName) => (
        <section className="database-schema-group" key={schemaName || "default"}>
          <div className="database-schema-heading">
            <Icon name="schema" size={13} />
            <span>{schemaName || "Default schema"}</span>
            <span className="database-schema-count">{tablesBySchema.get(schemaName).length}</span>
          </div>
          {tablesBySchema.get(schemaName)
            .slice()
            .sort((left, right) => left.name.localeCompare(right.name))
            .map((table) => {
              const isSelected = table.name === selectedTable && table.schema === selectedSchema;
              return (
                <button
                  className={`database-table-item${isSelected ? " active" : ""}`}
                  key={`${table.schema}.${table.name}`}
                  onClick={() => dispatch(selectDbTable({ name: table.name, schema: table.schema }))}
                  aria-current={isSelected ? "page" : undefined}
                >
                  <Icon name="table" size={15} />
                  <span>{table.name}</span>
                  <Icon name="chevron" size={14} />
                </button>
              );
            })}
        </section>
      ))}
      {schemaStatus === "succeeded" && schema.tables.length === 0 && <p className="database-message">No tables are available.</p>}
    </aside>
  );
}
