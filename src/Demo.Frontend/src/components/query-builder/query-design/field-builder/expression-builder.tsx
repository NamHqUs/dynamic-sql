import type { MetadataEntity, QueryExpression } from "../query-builder-slice";
import type { FunctionDefinition } from "../../../metadata-builder/function-definition/types";

type ExpressionBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  initialKind?: "binary" | "function";
  initialExpression?: QueryExpression;
  onApply: (expression: QueryExpression) => void;
  onCancel: () => void;
};

export function formatExpression(expression: QueryExpression): string {
  switch (expression.kind) {
    case "field":
      return expression.field || "Select field";
    case "value":
      return String(expression.value);
    case "binary":
      return `(${formatExpression(expression.arguments[0])} ${expression.operator} ${formatExpression(expression.arguments[1])})`;
    case "function":
      return `${expression.function}(${expression.arguments.map(formatExpression).join(", ")})`;
  }
}

export default function ExpressionBuilder(_props: ExpressionBuilderProps) {
  return null;
}
