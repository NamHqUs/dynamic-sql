import Icon from "../../icon/icon";
import "./data-editor.css";

export default function DataEditor({ text, error, rows, showPreview, onChange, onApply, onEdit, showApplyButton = true, showEditButton = true }) {
  const headers = Object.keys(rows[0] || {});

  if (showPreview) {
    return (
      <>
        <div className="data-preview">
          <div className="data-preview-head">
            <div><span className="section-kicker">DATA PREVIEW</span><strong>{rows.length} rows</strong></div>
            <span className="preview-ready"><Icon name="check" size={13} /> Applied</span>
          </div>
          <div className="preview-table-wrap">
            <table className="preview-table">
              <thead><tr>{headers.map((header) => <th key={header}>{header}</th>)}</tr></thead>
              <tbody>{rows.map((row, index) => <tr key={row.id ?? index}>{headers.map((header) => <td key={header}>{String(row[header] ?? "")}</td>)}</tr>)}</tbody>
            </table>
          </div>
        </div>
        {showEditButton && <div className="heading-actions"><button className="button secondary" onClick={onEdit}><Icon name="edit" /> Edit data</button></div>}
      </>
    );
  }

  return (
    <>
      <textarea className="data-editor" spellCheck="false" rows={14} value={text} onChange={(event) => onChange(event.target.value)} placeholder={"id,name,amount\n1,Alpha,100"} />
      {error && <div className="empty-state">{error}</div>}
      {showApplyButton && <div className="heading-actions"><button className="button primary" onClick={onApply}><Icon name="check" /> Apply Data</button></div>}
    </>
  );
}
