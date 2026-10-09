import { useRef } from "react";
import Icon from "../../icon/icon";
import DataEditor from "../data-editor/data-editor";
import "./upload-csv.css";

export default function UploadCsv({ text, error, rows, sourceName, showPreview, onChange, onApply, onEdit, onUpload }) {
  const fileInput = useRef(null);

  const handleFile = (event) => {
    const file = event.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => onUpload(String(reader.result), file.name.replace(/\.csv$/i, ""));
    reader.readAsText(file);
    event.target.value = "";
  };

  return (
    <div className="source-panel upload-csv">
      <div className="source-copy">
        <span className="section-kicker">CSV UPLOAD</span>
        <h2>Review and edit uploaded data</h2>
        <p>First line is the header row; values are comma-separated.</p>
        <button className="button secondary" onClick={() => fileInput.current?.click()}><Icon name="upload" /> Choose CSV file</button>
        <input ref={fileInput} type="file" accept=".csv,text/csv" hidden onChange={handleFile} />
      </div>
      <div className="source-editor">
        <DataEditor text={text} error={error} rows={rows} showPreview={showPreview} onChange={onChange} onApply={() => onApply(sourceName)} onEdit={onEdit} />
      </div>
    </div>
  );
}
