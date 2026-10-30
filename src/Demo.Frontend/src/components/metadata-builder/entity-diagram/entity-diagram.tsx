import { useState, type ReactNode } from "react";
import type { MetadataEntity } from "../../query-builder/query-design/query-builder-slice";
import DiagramView from "../../data-sources/diagram-view/diagram-view";
import DiagramSplitter from "../../data-sources/diagram-splitter/diagram-splitter";
import "./entity-diagram.css";

type EntityDiagramProps = {
  entities: MetadataEntity[];
  onOpenEntity: (table: { schema: string; name: string; columns: { name: string; dataType: string; isPrimaryKey: boolean }[] }) => void;
  entityEditor: ReactNode;
};

export default function EntityDiagram({ onOpenEntity, entityEditor }: EntityDiagramProps) {
  const [diagramHeight, setDiagramHeight] = useState(420);

  return (
    <div className="metadata-diagram-mode-content" style={{ "--diagram-panel-height": `${diagramHeight}px` } as React.CSSProperties}>
      <DiagramView onOpenTable={onOpenEntity} />
      <DiagramSplitter
        height={diagramHeight}
        minHeight={360}
        onResize={(height) => setDiagramHeight(Math.max(360, height))}
      />
      <section className="metadata-diagram-editor" aria-label="Entity definition editor">
        {entityEditor}
      </section>
    </div>
  );
}
