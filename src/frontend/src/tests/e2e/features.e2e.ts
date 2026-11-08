import { expect, request, test } from "@playwright/test";

import { BASE_URL } from "./support.ts";

/**
 * Anonymous capability report, pinned at the wire (not the DOM): exactly the five shipped camelCase
 * flags, all on in the default dev boot. A dedicated API context carries the self-contained suite's
 * BFF base URL.
 */
test("features endpoint reports the five flags all true", async () => {
  const api = await request.newContext({
    baseURL: BASE_URL,
    ignoreHTTPSErrors: true,
  });
  try {
    const response = await api.get("/api/v1/features");
    expect(response.status()).toBe(200);
    const flags = (await response.json()) as Record<string, boolean>;
    expect(Object.keys(flags).sort()).toEqual([
      "authentication",
      "csvExport",
      "excelExport",
      "health",
      "logging",
    ]);
    for (const value of Object.values(flags)) {
      expect(value).toBe(true);
    }
  } finally {
    await api.dispose();
  }
});
