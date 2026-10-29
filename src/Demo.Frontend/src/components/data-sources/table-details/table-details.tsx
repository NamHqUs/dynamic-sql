import { useDispatch, useSelector } from "react-redux";
import Icon from "../../icon/icon";
import { selectDbTable } from "../database-slice";
import "./table-details.css";

export default function TableDetails() {
  const dispatch = useDispatch();
  const { schema, selectedTable, selectedSchema } = useSelector((state) => state.database);
  const table = schema.tables.find((item) => item.name === selectedTable && item.schema === selectedSchema);
  if (!table) return null;

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
        <div>
          <span className="section-kicker">TABLE DETAILS</span>
          <h3><span>{table.schema || "Default schema"}</span><Icon name="chevron" size={13} />{table.name}</h3>
        </div>
        <span className="table-details-count">{table.columns.length} columns</span>
      </div>

      <div className="table-details-columns">
        <div className="table-details-section-title">Columns</div>
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

      <div className="table-details-relations">
        <div className="table-details-section-title">Relations</div>
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
    </section>
  );
}
