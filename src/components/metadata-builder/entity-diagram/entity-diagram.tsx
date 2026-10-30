import type { MetadataEntity } from "../../query-builder/query-design/query-builder-slice";
import DiagramView from "../../data-sources/diagram-view/diagram-view";

type EntityDiagramProps = {
  entities: MetadataEntity[];
  onOpenEntity: (table: { schema: string; name: string; columns: { name: string; dataType: string; isPrimaryKey: boolean }[] }) => void;
};

export default function EntityDiagram({ onOpenEntity }: EntityDiagramProps) {
  return <DiagramView onOpenTable={onOpenEntity} />;
}
