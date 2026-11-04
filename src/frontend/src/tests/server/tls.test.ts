import { afterEach, describe, expect, test } from "bun:test";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

import type { CockpitConfig } from "../../server/config.ts";
import { loadTls } from "../../server/index.ts";

const base: CockpitConfig = { port: 3000, dotnetApiUrl: "https://localhost:5208" };
let scratch: string | undefined;

afterEach(async () => {
  if (scratch !== undefined) {
    await rm(scratch, { recursive: true, force: true });
    scratch = undefined;
  }
});

describe("loadTls", () => {
  test("returns undefined when no keypair is configured", async () => {
    await expect(loadTls(base)).resolves.toBeUndefined();
  });

  test("throws with the fix when a configured file is missing", async () => {
    await expect(
      loadTls({
        ...base,
        tlsCertFile: "./.certs/nope-cert.pem",
        tlsKeyFile: "./.certs/nope-key.pem",
      }),
    ).rejects.toThrow("BFF_TLS_CERT_FILE not found");
  });

  test("loads an existing keypair", async () => {
    scratch = await mkdtemp(join(tmpdir(), "bff-tls-"));
    const cert = join(scratch, "cert.pem");
    const key = join(scratch, "key.pem");
    await Bun.write(cert, "cert-bytes");
    await Bun.write(key, "key-bytes");

    const tls = await loadTls({ ...base, tlsCertFile: cert, tlsKeyFile: key });
    expect(tls?.cert.name).toContain("cert.pem");
    expect(tls?.key.name).toContain("key.pem");
  });
});
