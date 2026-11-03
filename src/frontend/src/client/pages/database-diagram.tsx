import { useMemo } from "react";

import { FlowCanvas } from "../components/FlowCanvas.tsx";
import type { DbTableSchema } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
import { buildErNodesEdges } from "../lib/flow-graphs.ts";

/**
 * The single database diagram. The entity lane is drawn from live schemas: one node per table, a
 * solid edge per foreign key, a dashed stub per table carrying source_key. The flow lane shows data
 * entering through runs and leaving through postings windows and exports.
 */
export function ErFlowDiagram({
  tables,
  schemas,
}: {
  tables: string[];
  schemas: Record<string, DbTableSchema>;
}) {
  useRenderLog("ErFlowDiagram");
  const { nodes, edges } = useMemo(
    () => buildErNodesEdges(tables, schemas),
    [tables, schemas],
  );
  return (
    <FlowCanvas
      nodes={nodes}
      edges={edges}
      label="Entity relationships and data flow"
      testId="er-flow"
    />
  );
}
