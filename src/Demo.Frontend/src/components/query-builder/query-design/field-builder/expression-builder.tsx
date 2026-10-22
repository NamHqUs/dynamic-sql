import type { IFieldDefinition, MetadataEntity } from "../query-builder-slice";
import type { FunctionDefinition } from "../../../metadata-builder/function-definition/types";

type ExpressionBuilderProps = {
  entity: MetadataEntity;
  metadata: MetadataEntity[];
  functionDefinitions: FunctionDefinition[];
  functionDefinitionsStatus: string;
  functionDefinitionsError: string;
  initialKind?: "group" | "function";
  initialExpression?: IFieldDefinition;
  onApply: (expression: IFieldDefinition) => void;
  onCancel: () => void;
};

export function formatExpression(expression: IFieldDefinition): string {
  const formatOperand = (operand: IFieldDefinition) => operand.kind === "group"
    ? `(${formatExpression(operand)})`
    : formatExpression(operand);

  switch (expression.kind) {
    case "field":
      return expression.field || "Select field";
    case "value":
      return String(expression.value);
    case "math":
      return `${expression.oper} ${formatOperand(expression.field)}`;
    case "group":
      return `(${expression.arguments.map((argument, index) => {
        if (index === 0) return formatOperand(argument);
        if (argument.kind === "math")
          return `${argument.oper} ${formatOperand(argument.field)}`;
        return `${expression.operator ?? "+"} ${formatOperand(argument)}`;
      }).join(" ")})`;
    case "function":
      return `${expression.function}(${expression.arguments.map(formatExpression).join(", ")})`;
  }
}

export default function ExpressionBuilder(_props: ExpressionBuilderProps) {
  return null;
}
