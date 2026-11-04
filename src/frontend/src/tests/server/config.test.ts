import { describe, expect, test } from "bun:test";

import { parseConfig } from "../../server/config.ts";

describe("parseConfig", () => {
  test("applies defaults for an empty env", () => {
    expect(parseConfig({})).toEqual({
      port: 3000,
      dotnetApiUrl: "https://localhost:5208",
      tlsCertFile: undefined,
      tlsKeyFile: undefined,
    });
  });

  test("accepts explicit values and strips trailing slashes", () => {
    expect(parseConfig({ PORT: "3100", DOTNET_API_URL: "http://127.0.0.1:5207///" })).toEqual({
      port: 3100,
      dotnetApiUrl: "http://127.0.0.1:5207",
      tlsCertFile: undefined,
      tlsKeyFile: undefined,
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

  test("accepts a TLS keypair when both files are set", () => {
    expect(
      parseConfig({
        BFF_TLS_CERT_FILE: "./.certs/localhost-cert.pem",
        BFF_TLS_KEY_FILE: "./.certs/localhost-key.pem",
      }),
    ).toEqual({
      port: 3000,
      dotnetApiUrl: "https://localhost:5208",
      tlsCertFile: "./.certs/localhost-cert.pem",
      tlsKeyFile: "./.certs/localhost-key.pem",
    });
  });

  test("rejects a half-configured TLS keypair", () => {
    expect(() => parseConfig({ BFF_TLS_CERT_FILE: "./.certs/localhost-cert.pem" })).toThrow(
      "Invalid TLS config",
    );
    expect(() => parseConfig({ BFF_TLS_KEY_FILE: "./.certs/localhost-key.pem" })).toThrow(
      "Invalid TLS config",
    );
  });

  test("treats empty TLS paths as unset", () => {
    expect(parseConfig({ BFF_TLS_CERT_FILE: "", BFF_TLS_KEY_FILE: "" })).toEqual({
      port: 3000,
      dotnetApiUrl: "https://localhost:5208",
      tlsCertFile: undefined,
      tlsKeyFile: undefined,
    });
  });
});
