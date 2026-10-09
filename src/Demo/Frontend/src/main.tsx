import { useEffect, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { Provider, useDispatch, useSelector } from "react-redux";
import Icon from "./components/icon/icon";
import DataSources from "./components/data-sources/data-sources";
import { fetchDataSource, setDataSource } from "./components/data-sources/data-source-slice";
import { store } from "./store/store";
import "./styles.css";

const seedFields = [
  { key: "id", label: "Customer ID", type: "integer" },
  { key: "customer", label: "Customer", type: "string" },
  { key: "segment", label: "Segment", type: "string" },
  { key: "region", label: "Region", type: "string" },
  { key: "revenue", label: "Revenue", type: "currency" },
  { key: "orders", label: "Orders", type: "integer" },
  { key: "status", label: "Status", type: "string" },
  { key: "updated", label: "Last updated", type: "date" },
];

function inferFields(rows) {
  if (!rows.length) return [];
  return Object.keys(rows[0]).map((key) => seedFields.find((field) => field.key === key) || { key, label: key, type: typeof rows[0][key] === "number" ? "integer" : "string" });
}

function App() {
  const dispatch = useDispatch();
  const { rows, sourceName, error: dataSourceError } = useSelector((state) => state.dataSource);
  const [page, setPage] = useState("sources");
  const fields = useMemo(() => inferFields(rows), [rows]);
  const [selectedFields, setSelectedFields] = useState(seedFields.map(({ key }) => key));
  const [quickFilter, setQuickFilter] = useState("");
  const [filter, setFilter] = useState({ field: "region", operator: "equals", value: "North America" });
  const [filterEnabled, setFilterEnabled] = useState(true);
  const [aggregate, setAggregate] = useState("none");

  useEffect(() => {
    const url = import.meta.env.VITE_DATA_SOURCE_URL;
    if (url) dispatch(fetchDataSource(url));
  }, [dispatch]);

  const filteredRows = useMemo(() => rows.filter((row) => {
    const searchMatch = !quickFilter || Object.values(row).some((value) => String(value).toLowerCase().includes(quickFilter.toLowerCase()));
    if (!filterEnabled || !filter.value) return searchMatch;
    const actual = row[filter.field];
    const expected = filter.field === "revenue" || filter.field === "orders" ? Number(filter.value) : filter.value.toLowerCase();
    const normalized = typeof actual === "string" ? actual.toLowerCase() : actual;
    const filterMatch = filter.operator === "contains" ? String(normalized).includes(String(expected)) : filter.operator === "greater" ? normalized > expected : filter.operator === "less" ? normalized < expected : normalized === expected;
    return searchMatch && filterMatch;
  }), [rows, quickFilter, filter, filterEnabled]);

  const toggleField = (key) => setSelectedFields((current) => current.includes(key) ? current.filter((field) => field !== key) : [...current, key]);
  const applyData = (parsed, name) => {
    dispatch(setDataSource({ rows: parsed, name }));
    const keys = Object.keys(parsed[0]);
    setSelectedFields(keys);
    setFilter({ field: keys[0], operator: "equals", value: "" });
    setFilterEnabled(false);
  };

  const displayFields = fields.filter(({ key }) => selectedFields.includes(key));
  const totalRevenue = filteredRows.reduce((sum, row) => sum + (Number(row.revenue) || 0), 0);

  return (
    <main className="app-shell">
      <header className="topbar">
        <div className="brand"><span className="brand-mark"><Icon name="database" size={18} /></span><span>Dynamic<span className="brand-accent">SQL</span></span><span className="brand-divider" /><span className="brand-product">Studio</span></div>
        <nav className="topnav"><button className={page === "sources" ? "nav-item active" : "nav-item"} onClick={() => setPage("sources")}><Icon name="database" /> Data sources</button><button className={page === "builder" ? "nav-item active" : "nav-item"} onClick={() => setPage("builder")}><Icon name="grid" /> Query builder</button></nav>
        <div className="top-actions"><span className="live-dot" /> Local demo <button className="avatar">N</button></div>
      </header>

      {page === "sources" ? (
        <DataSources />
      ) : (
      <>
      <section className="page-heading">
        <div><div className="eyebrow">WORKSPACE / QUERY BUILDER</div><h1>Explore your data</h1><p>Compose a query from metadata, then inspect the result in real time.</p></div>
        <div className="heading-actions"><button className="button secondary"><Icon name="download" /> Export</button><button className="button primary" onClick={() => setQuickFilter("")}><Icon name="play" /> Run query</button></div>
      </section>

      <section className="workspace-card">
        <div className="tabs"><button className="tab active"><span className="tab-number">01</span> Define query</button><span className="tab-line" /><span className="query-status"><span className="status-check"><Icon name="check" size={13} /></span> Query ready</span></div>
        {(
          <div className="define-panel"><div className="query-pill"><Icon name="database" size={14} /><span>FROM</span><strong>{sourceName}</strong><span className="pill-dot">·</span><span>{rows.length} records available</span></div><div className="define-grid"><div><label>Projection</label><p className="helper">Select fields to include in the result.</p><div className="field-chips">{fields.map((field) => <button key={field.key} className={selectedFields.includes(field.key) ? "field-chip selected" : "field-chip"} onClick={() => toggleField(field.key)}>{selectedFields.includes(field.key) && <Icon name="check" size={12} />}{field.label}</button>)}</div></div><div><label>Aggregate</label><p className="helper">Optional summary over the result set.</p><select value={aggregate} onChange={(event) => setAggregate(event.target.value)}><option value="none">No aggregate</option><option value="revenue">SUM(revenue)</option><option value="orders">SUM(orders)</option></select></div></div></div>
        )}
      </section>

      <section className="results-card">
        <div className="results-head"><div><div className="section-kicker">RESULTS <span className="result-count">{filteredRows.length} of {rows.length}</span></div><h2>Customer revenue overview</h2></div><div className="result-tools"><div className="search-box"><Icon name="search" size={15} /><input value={quickFilter} onChange={(event) => setQuickFilter(event.target.value)} placeholder="Search results..." /></div><button className="icon-button" title="Export result"><Icon name="download" size={16} /></button></div></div>
        <div className="filter-bar"><span className="filter-label"><Icon name="sliders" size={14} /> FILTER</span><select value={filter.field} onChange={(event) => setFilter({ ...filter, field: event.target.value })}>{fields.filter((field) => field.type !== "date").map((field) => <option key={field.key} value={field.key}>{field.label}</option>)}</select><select value={filter.operator} onChange={(event) => setFilter({ ...filter, operator: event.target.value })}><option value="equals">is</option><option value="contains">contains</option><option value="greater">greater than</option><option value="less">less than</option></select><input value={filter.value} onChange={(event) => setFilter({ ...filter, value: event.target.value })} placeholder="Value..." /><button className={filterEnabled ? "filter-toggle on" : "filter-toggle"} onClick={() => setFilterEnabled(!filterEnabled)}>{filterEnabled ? "Applied" : "Apply filter"}</button><button className="clear-filter" onClick={() => { setFilterEnabled(false); setFilter({ ...filter, value: "" }); }}>Clear</button></div>
        <div className="table-wrap"><table><thead><tr>{displayFields.map((field) => <th key={field.key}>{field.label}<span className="sort-mark">↕</span></th>)}</tr></thead><tbody>{filteredRows.map((row) => <tr key={row.id}>{displayFields.map((field) => <td key={field.key} className={field.key === "revenue" ? "money" : ""}>{field.key === "revenue" ? `$${Number(row[field.key]).toLocaleString()}` : field.key === "status" ? <span className={`status status-${String(row[field.key]).toLowerCase()}`}><i />{row[field.key]}</span> : row[field.key]}</td>)}</tr>)}</tbody></table>{filteredRows.length === 0 && <div className="empty-state">No rows match this filter. Try a different value.</div>}</div>
        <div className="results-foot"><span>Showing <b>{filteredRows.length}</b> records</span>{aggregate !== "none" && <span className="aggregate-result">{aggregate === "revenue" ? "SUM(revenue)" : "SUM(orders)"} <b>{aggregate === "revenue" ? `$${totalRevenue.toLocaleString()}` : filteredRows.reduce((sum, row) => sum + Number(row.orders || 0), 0).toLocaleString()}</b></span>}<span className="foot-spacer" /><span>Last run just now</span></div>
      </section>
      </>
      )}
      <footer><span><span className="footer-mark">✦</span> DynamicSQL engine</span><span>Metadata-driven queries · Client preview</span></footer>
    </main>
  );
}

createRoot(document.getElementById("root")).render(<Provider store={store}><App /></Provider>);
