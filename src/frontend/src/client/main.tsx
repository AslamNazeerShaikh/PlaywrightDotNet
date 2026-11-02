import { createRoot } from "react-dom/client";

import { App } from "./app.tsx";

const root = document.getElementById("root");
if (root === null) {
  throw new Error("Missing #root element in index.html.");
}
createRoot(root).render(<App id="main-app-1"/>);
