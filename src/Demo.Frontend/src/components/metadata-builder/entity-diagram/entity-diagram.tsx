import { useMemo, useState } from "react";
import { useSelector } from "react-redux";
import Icon from "../../icon/icon";
import type { MetadataEntity } from "../../query-builder/query-design/query-builder-slice";
import "./entity-diagram.css";

type SchemaTable = {
  schema: string;
  name: string;
  columns: { name: string; dataType: string; isPrimaryKey: boolean }[];
};

type BusinessDomain = {
  name: string;
  tableKeys: string[];
};

type EntityDiagramProps = {
  entities: MetadataEntity[];
  onOpenEntity: (table: SchemaTable) => void;
};

const STORAGE_KEY = "dynamicsql.entity-diagram.domains";

function keyFor(table: SchemaTable) {
  return `${table.schema}.${table.name}`;
}

function readDomains(): BusinessDomain[] {
  try {
    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY) || "[]");
    return Array.isArray(stored) && stored.every((domain) => typeof domain?.name === "string" && Array.isArray(domain.tableKeys))
      ? stored
      : [];
  } catch {
    return [];
  }
}

export default function EntityDiagram({ entities, onOpenEntity }: EntityDiagramProps) {
  const { schema, schemaStatus, schemaError } = useSelector((state) => state.database);
  const [domains, setDomains] = useState<BusinessDomain[]>(readDomains);
  const [selectedDomain, setSelectedDomain] = useState("");
  const [domainName, setDomainName] = useState("");
  const [tableToAdd, setTableToAdd] = useState("");

  const activeDomain = domains.find((domain) => domain.name === selectedDomain) || domains[0];
  const activeTables = useMemo(
    () => schema.tables.filter((table) => activeDomain?.tableKeys.includes(keyFor(table))),
    [activeDomain, schema.tables],
  );
  const availableTables = schema.tables.filter((table) => !activeDomain?.tableKeys.includes(keyFor(table)));
  const entityNames = new Set(entities.map((entity) => entity.name));
  const activeEntityNames = new Set(activeTables.map((table) => table.name));
  const activeRelationships = entities.flatMap((entity) => (entity.relations || [])
    .filter((relation) => activeEntityNames.has(entity.name) && activeEntityNames.has(relation.relatedEntity))
    .map((relation) => ({ from: entity.name, to: relation.relatedEntity, name: relation.name })));

  function persist(nextDomains: BusinessDomain[]) {
    setDomains(nextDomains);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(nextDomains));
  }

  function createDomain(event: React.FormEvent) {
    event.preventDefault();
    const name = domainName.trim();
    if (!name || domains.some((domain) => domain.name.toLowerCase() === name.toLowerCase())) return;
    const next = [...domains, { name, tableKeys: [] }];
    persist(next);
    setSelectedDomain(name);
    setDomainName("");
  }

  function addTable() {
    if (!activeDomain || !tableToAdd) return;
    persist(domains.map((domain) => domain.name === activeDomain.name
      ? { ...domain, tableKeys: [...domain.tableKeys, tableToAdd] }
      : domain));
    setTableToAdd("");
  }

  function removeTable(tableKey: string) {
    if (!activeDomain) return;
    persist(domains.map((domain) => domain.name === activeDomain.name
      ? { ...domain, tableKeys: domain.tableKeys.filter((key) => key !== tableKey) }
      : domain));
  }

  if (schemaStatus === "loading" || schemaStatus === "idle") {
    return <div className="entity-diagram-state"><Icon name="schema" size={20} /><span>Loading database tables…</span></div>;
  }
  if (schemaStatus === "failed") {
    return <p className="entity-diagram-state error" role="alert">{schemaError || "Unable to load database tables."}</p>;
  }

  return (
    <section className="entity-diagram" aria-label="Business domain entity diagram">
      <div className="entity-diagram-toolbar">
        <div>
          <span className="section-kicker">BUSINESS DOMAINS</span>
          <p>Create a domain board, add database tables, then select a table to define its entity and relationships.</p>
        </div>
        <form className="entity-domain-create" onSubmit={createDomain}>
          <input value={domainName} onChange={(event) => setDomainName(event.target.value)} placeholder="New domain, e.g. HR" aria-label="New business domain" />
          <button className="button compact" type="submit"><Icon name="plus" size={13} /> Add domain</button>
        </form>
      </div>
      <div className="entity-diagram-domain-bar">
        <nav className="entity-domain-list" aria-label="Business domains">
          {domains.map((domain) => (
            <button type="button" className={activeDomain?.name === domain.name ? "active" : ""} key={domain.name} onClick={() => setSelectedDomain(domain.name)}>
              <Icon name="schema" size={14} /><strong>{domain.name}</strong><span>{domain.tableKeys.length}</span>
            </button>
          ))}
          {!domains.length && <span className="entity-diagram-empty">Create your first business domain.</span>}
        </nav>
        {activeDomain && (
          <div className="entity-diagram-add-table">
            <select value={tableToAdd} onChange={(event) => setTableToAdd(event.target.value)} aria-label="Database table to add">
              <option value="">Add database table…</option>
              {availableTables.map((table) => <option value={keyFor(table)} key={keyFor(table)}>{keyFor(table)}</option>)}
            </select>
            <button className="button compact" type="button" onClick={addTable} disabled={!tableToAdd}><Icon name="plus" size={13} /> Add table</button>
          </div>
        )}
      </div>
      {!activeDomain ? (
        <div className="entity-diagram-state"><Icon name="schema" size={22} /><span>Create a business domain to start a diagram board.</span></div>
      ) : (
        <div className="entity-diagram-board">
          {activeTables.map((table, index) => {
            const entityDefined = entityNames.has(table.name);
            return (
              <article className="entity-diagram-card" key={keyFor(table)} style={{ "--card-index": index } as React.CSSProperties}>
                <div className="entity-diagram-card-head">
                  <button type="button" onClick={() => onOpenEntity(table)} title={`Define ${table.name} entity`}>
                    <Icon name="table" size={14} /><strong>{table.name}</strong>
                  </button>
                  <button type="button" className="icon-button subtle" onClick={() => removeTable(keyFor(table))} aria-label={`Remove ${table.name} from ${activeDomain.name}`}><Icon name="close" size={13} /></button>
                </div>
                <div className="entity-diagram-card-schema">{table.schema}</div>
                <div className="entity-diagram-card-fields">
                  {table.columns.slice(0, 4).map((column) => <span key={column.name}><Icon name={column.isPrimaryKey ? "key" : "field"} size={11} />{column.name}</span>)}
                  {table.columns.length > 4 && <small>+{table.columns.length - 4} more</small>}
                </div>
                <button type="button" className={`entity-diagram-define${entityDefined ? " defined" : ""}`} onClick={() => onOpenEntity(table)}>
                  {entityDefined ? "Edit entity definition" : "Define entity & relationships"} <Icon name="arrow-right" size={12} />
                </button>
              </article>
            );
          })}
          {!activeTables.length && <div className="entity-diagram-board-empty"><Icon name="table" size={22} /><span>Add database tables to this domain board.</span></div>}
        </div>
      )}
      {activeDomain && activeRelationships.length > 0 && <p className="entity-diagram-relation-summary"><Icon name="schema" size={13} /> {activeRelationships.length} entity relationship{activeRelationships.length === 1 ? "" : "s"} on this board: {activeRelationships.map((relation) => `${relation.from} → ${relation.to}`).join(", ")}</p>}
    </section>
  );
}
