import { useEffect, useState } from "react";
import Icon from "../../icon/icon";
import PanelSplitter from "../../panel-splitter/panel-splitter";
import { useMessageBox } from "../../message-box/message-box";
import { useResizablePanel } from "../../../utils/use-resizable-panel";
import DefinitionActions, {
  resolveDefinitionChanges,
  useDefinitionUnsavedWarning,
} from "../definition-actions/definition-actions";
import type { DefinitionActionAdapter } from "../definition-actions/definition-actions";
import MetadataSidebar, { type MetadataSidebarGroup } from "../metadata-sidebar/metadata-sidebar";
import "./operator-definition.css";

type OperatorDefinitionData = {
  id: string;
  name: string;
  symbol: string;
  description: string;
  leftOperandType: string;
  rightOperandType: string;
};

type OperatorDraft = Omit<OperatorDefinitionData, "id"> & { id: string | null };

const operandTypes = ["String", "Number", "Datetime", "Boolean"];
let nextOperatorId = 1;
type NavigationGuard = (continueNavigation: () => void, cancelNavigation?: () => void) => void;
type OperatorDefinitionProps = {
  onNavigationGuardChange?: (guard: NavigationGuard | null) => void;
};

function emptyOperator(): OperatorDraft {
  return {
    id: null,
    name: "",
    symbol: "",
    description: "",
    leftOperandType: "String",
    rightOperandType: "String",
  };
}

export default function OperatorDefinition({ onNavigationGuardChange }: OperatorDefinitionProps) {
  const showMessageBox = useMessageBox();
  const panel = useResizablePanel("metadata-operators");
  const [definitions, setDefinitions] = useState<OperatorDefinitionData[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [draft, setDraft] = useState<OperatorDraft | null>(null);
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState("");
  const [filter, setFilter] = useState("");
  const selectedDefinition = definitions.find((definition) => definition.id === selectedId);
  const selectedName = selectedDefinition?.name || "";

  useDefinitionUnsavedWarning(dirty);

  const discardChanges = () => {
    setDraft(selectedDefinition ? { ...selectedDefinition } : null);
    setDirty(false);
    setMessage("");
  };

  const save = async (): Promise<boolean> => {
    if (!draft?.name.trim() || !draft.symbol.trim()) {
      setMessage("Operator name and symbol are required.");
      return false;
    }
    const name = draft.name.trim();
    if (definitions.some((definition) =>
      definition.id !== draft.id && definition.name.toLowerCase() === name.toLowerCase(),
    )) {
      setMessage("An operator with this name already exists.");
      return false;
    }

    setSaving(true);
    try {
      const saved: OperatorDefinitionData = {
        ...draft,
        id: draft.id || `operator-${nextOperatorId++}`,
        name,
        symbol: draft.symbol.trim(),
        description: draft.description.trim(),
      };
      setDefinitions((current) => {
        const existingIndex = current.findIndex((definition) => definition.id === saved.id);
        if (existingIndex === -1) return [...current, saved];
        return current.map((definition) => definition.id === saved.id ? saved : definition);
      });
      setSelectedId(saved.id);
      setDraft(saved);
      setDirty(false);
      setMessage("");
      return true;
    } finally {
      setSaving(false);
    }
  };

  const create = () => {
    setSelectedId("");
    setDraft(emptyOperator());
    setDirty(true);
    setMessage("");
  };

  const remove = async () => {
    if (!selectedId) return;
    setDefinitions((current) => current.filter((definition) => definition.id !== selectedId));
    setSelectedId("");
    setDraft(null);
    setDirty(false);
    setMessage("");
  };

  const actionAdapter: DefinitionActionAdapter = {
    resourceName: "operator",
    selectedName,
    hasSelection: Boolean(selectedId),
    isBuiltIn: false,
    hasDraft: Boolean(draft),
    dirty,
    saving,
    onSave: save,
    onDiscard: discardChanges,
    onDelete: remove,
    onNew: create,
  };

  const resolveDirtyChanges = async () => resolveDefinitionChanges(actionAdapter, showMessageBox);

  useEffect(() => {
    const guard: NavigationGuard = (continueNavigation, cancelNavigation) => {
      void (async () => {
        if (await resolveDirtyChanges()) continueNavigation();
        else cancelNavigation?.();
      })();
    };
    onNavigationGuardChange?.(guard);
    return () => onNavigationGuardChange?.(null);
  }, [onNavigationGuardChange, dirty, draft, definitions, selectedId]);

  const selectOperator = async (definition: OperatorDefinitionData) => {
    if (definition.id === selectedId || !(await resolveDirtyChanges())) return;
    setSelectedId(definition.id);
    setDraft({ ...definition });
    setDirty(false);
    setMessage("");
  };

  const updateDraft = (update: (current: OperatorDraft) => OperatorDraft) => {
    if (!draft) return;
    setDraft(update(draft));
    setDirty(true);
  };

  const visibleDefinitions = definitions.filter((definition) =>
    `${definition.name} ${definition.symbol}`.toLowerCase().includes(filter.toLowerCase()),
  );
  const renderDefinition = (definition: OperatorDefinitionData) => (
    <button
      className={`metadata-entity${definition.id === selectedId ? " active" : ""}`}
      key={definition.id}
      type="button"
      onClick={() => void selectOperator(definition)}
    >
      <span className="entity-icon"><Icon name="code" size={14} /></span>
      <span>
        <b>{definition.name}</b>
        <small>{definition.symbol} · {definition.leftOperandType} → {definition.rightOperandType}</small>
      </span>
      <Icon name="chevron" size={14} />
    </button>
  );
  const groups: MetadataSidebarGroup[] = [
    {
      id: "operator-custom-list",
      title: "Custom",
      count: visibleDefinitions.length,
      content: visibleDefinitions.map(renderDefinition),
      emptyMessage: definitions.length
        ? "No matching custom operators."
        : "No custom operators yet.",
    },
    {
      id: "operator-builtin-list",
      title: "Builtin",
      count: 0,
      content: null,
      emptyMessage: "No built-in operators.",
    },
  ];

  return (
    <section
      className={`operator-workspace split-workspace${panel.collapsed ? " panel-collapsed" : ""}`}
      style={panel.style}
    >
      <MetadataSidebar
        className="operator-list split-panel"
        title="OPERATORS"
        totalCount={definitions.length}
        filter={filter}
        searchPlaceholder="Find an operator"
        groups={groups}
        onFilterChange={setFilter}
      />
      <PanelSplitter panel={panel} label="operators panel" />
      <main className="operator-editor">
        <div className="editor-head">
          <div>
            <span className="section-kicker">{selectedName || (draft ? "New Operator…" : "Select an operator")}</span>
          </div>
          <DefinitionActions adapter={actionAdapter} />
        </div>
        <p className="operator-notice">Preview only. Operator definitions are held in memory and are not saved to an API.</p>
        {!draft
          ? <div className="metadata-empty">
              <Icon name="code" size={32} />
              <p>{message || "Select an operator or create a new custom definition."}</p>
            </div>
          : <>
              {message && <p className="metadata-alert" role="alert">{message}</p>}
              <fieldset className="operator-form" disabled={saving}>
                <div className="metadata-form-grid">
                  <label>
                    Name
                    <input
                      readOnly={Boolean(selectedId)}
                      value={draft.name}
                      onChange={(event) => updateDraft((current) => ({ ...current, name: event.target.value }))}
                      placeholder="e.g. Contains"
                    />
                  </label>
                  <label>
                    Operator symbol
                    <input
                      value={draft.symbol}
                      onChange={(event) => updateDraft((current) => ({ ...current, symbol: event.target.value }))}
                      placeholder="e.g. CONTAINS"
                    />
                  </label>
                  <label className="operator-description">
                    Description
                    <input
                      value={draft.description}
                      onChange={(event) => updateDraft((current) => ({ ...current, description: event.target.value }))}
                      placeholder="Describe what this operator compares"
                    />
                  </label>
                  <label>
                    Left operand type
                    <select
                      value={draft.leftOperandType}
                      onChange={(event) => updateDraft((current) => ({ ...current, leftOperandType: event.target.value }))}
                    >
                      {operandTypes.map((type) => <option key={type} value={type}>{type}</option>)}
                    </select>
                  </label>
                  <label>
                    Right operand type
                    <select
                      value={draft.rightOperandType}
                      onChange={(event) => updateDraft((current) => ({ ...current, rightOperandType: event.target.value }))}
                    >
                      {operandTypes.map((type) => <option key={type} value={type}>{type}</option>)}
                    </select>
                  </label>
                </div>
                <section className="editor-section">
                  <div className="editor-section-head">
                    <div>
                      <h3>Provider templates</h3>
                      <p>Map the operator to SQL for each supported database provider.</p>
                    </div>
                  </div>
                  <div className="operator-template-preview" aria-hidden="true">
                    <span>PROVIDER</span>
                    <span>SQL TEMPLATE</span>
                    <span>ADD PROVIDER TEMPLATE</span>
                  </div>
                </section>
              </fieldset>
            </>}
      </main>
    </section>
  );
}
