import { describe, expect, test } from "bun:test";

import { parseRoute, routeHash } from "../../client/lib/router.ts";

describe("router", () => {
  test("parses each page hash", () => {
    expect(parseRoute("#/overview")).toBe("overview");
    expect(parseRoute("#/health")).toBe("health");
    expect(parseRoute("#/profiles")).toBe("profiles");
    expect(parseRoute("#/sources")).toBe("sources");
    expect(parseRoute("#/sessions")).toBe("sessions");
    expect(parseRoute("#/runs")).toBe("runs");
    expect(parseRoute("#/topology")).toBe("topology");
  });

  test("tolerates trailing slashes and surrounding whitespace", () => {
    expect(parseRoute("#/health/")).toBe("health");
    expect(parseRoute("  #/profiles  ")).toBe("profiles");
  });

  test("unknown, empty, and non-hash input falls home to overview", () => {
    for (const hash of ["", "#/", "#/nope", "/health", "#/HEALTH"]) {
      expect(parseRoute(hash)).toBe("overview");
    }
  });

  test("round-trips through routeHash", () => {
    expect(routeHash(parseRoute("#/topology"))).toBe("#/topology");
  });
});
