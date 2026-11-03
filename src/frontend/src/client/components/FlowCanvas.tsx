import { type Edge, type Node, ReactFlow } from "@xyflow/react";
import "@xyflow/react/dist/style.css";
import type { MouseEvent } from "react";

export interface FlowCanvasProps {
  nodes: Node[];
  edges: Edge[];
  height?: number;
  label: string;
  testId: string;
  fitView?: boolean;
  onNodeClick?: (event: MouseEvent, node: Node) => void;
}

/**
 * One React Flow wrapper for every deck diagram. The canvas owns the CSS import, an explicit-size
 * container for happy-dom, and an sr-only text mirror so node and edge labels stay in textContent
 * assertions.
 */
export function FlowCanvas({
  nodes,
  edges,
  height = 420,
  label,
  testId,
  fitView = true,
  onNodeClick,
}: FlowCanvasProps) {
  const animated = useFlowAnimated();
  const styledNodes = nodes.map((node) => ({
    ...node,
    style: {
      background: "var(--canvas)",
      color: "var(--ink)",
      border: "1px solid var(--line-hi)",
      borderRadius: 4,
      ...node.style,
    },
  }));
  const renderedEdges = edges.map((edge) =>
    edge.animated === true ? { ...edge, animated } : edge,
  );
  // A clickable canvas holds focusable nodes, so it is a group, not a static
  // image. The table list below it keeps the same actions keyboard-reachable.
  const role = onNodeClick === undefined ? "img" : "group";
  return (
    <div style={{ width: "100%", height }} role={role} aria-label={label} data-testid={testId}>
      <ReactFlow
        nodes={styledNodes}
        edges={renderedEdges}
        fitView={fitView}
        proOptions={{ hideAttribution: false }}
        panOnDrag
        zoomOnScroll
        nodesDraggable={false}
        nodesConnectable={false}
        elementsSelectable
        onNodeClick={onNodeClick}
      />
      <ul className="sr-only">
        {styledNodes.map((node) => (
          <li key={`node-${node.id}`}>{nodeLabelText(node)}</li>
        ))}
        {renderedEdges.map((edge) => (
          <li key={`edge-${edge.id}`}>{edgeLabelText(edge)}</li>
        ))}
      </ul>
    </div>
  );
}

/** False under prefers-reduced-motion; animated edges read it, static edges ignore it. */
export function useFlowAnimated(): boolean {
  if (typeof window === "undefined") {
    return true;
  }
  return window.matchMedia?.("(prefers-reduced-motion: reduce)")?.matches !== true;
}

function nodeLabelText(node: Node): string {
  const label = (node.data as { label?: unknown } | undefined)?.label;
  return typeof label === "string" ? label : node.id;
}

function edgeLabelText(edge: Edge): string {
  return typeof edge.label === "string" ? edge.label : "";
}
