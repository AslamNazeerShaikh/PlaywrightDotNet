import { describe, expect, test } from "bun:test";

import { parseConfig } from "../../server/config.ts";

describe("parseConfig", () => {
  test("applies defaults for an empty env", () => {
    expect(parseConfig({})).toEqual({
      port: 3000,
      dotnetApiUrl: "http://127.0.0.1:5207",
    });
  });

  test("accepts explicit values and strips trailing slashes", () => {
    expect(parseConfig({ PORT: "3100", DOTNET_API_URL: "http://127.0.0.1:5207///" })).toEqual({
      port: 3100,
      dotnetApiUrl: "http://127.0.0.1:5207",
    });
  });

  test("rejects non-numeric, fractional, and out-of-range ports", () => {
    for (const port of ["abc", "3.5", "0", "65536", "-1"]) {
      expect(() => parseConfig({ PORT: port })).toThrow("Invalid PORT");
    }
  });

  test("rejects relative and non-http(s) API urls", () => {
    expect(() => parseConfig({ DOTNET_API_URL: "/relative" })).toThrow("Invalid DOTNET_API_URL");
    expect(() => parseConfig({ DOTNET_API_URL: "ftp://host/api" })).toThrow(
      "Invalid DOTNET_API_URL",
    );
  });
});
