import Icon from "../../icon/icon";
import "./database-connection.css";

const dbProviders = [
  { name: "SQLite", hint: "Local file database" },
  { name: "SQL Server", hint: "Microsoft SQL Server" },
  { name: "PostgreSQL", hint: "Postgres server" },
];

export default function DatabaseConnection() {
  return (
    <div className="source-options">
      {dbProviders.map((db) => <button key={db.name} className="source-option" disabled title="Coming soon"><span className="option-icon"><Icon name="database" /></span><span><b>{db.name}</b><small>{db.hint} · Coming soon</small></span></button>)}
    </div>
  );
}
