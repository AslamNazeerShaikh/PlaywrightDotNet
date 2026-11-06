import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";

import { App } from "../../client/app.tsx";
import {
  createApiClient,
  type FetchImpl,
  type MeResponse,
  PERMISSION_CATALOG,
} from "../../client/lib/api.ts";
import {
  clearToken,
  hasAnyPermission,
  isTokenFailure,
  loadToken,
  saveToken,
} from "../../client/lib/auth.ts";
import { parseRoute } from "../../client/lib/router.ts";

beforeAll(() => {
  GlobalRegistrator.register();
});

afterAll(async () => {
  await new Promise((resolve) => setTimeout(resolve, 100));
  await GlobalRegistrator.unregister();
});

afterEach(() => {
  cleanup();
  sessionStorage.clear();
});

function stubJson(status: number, body: unknown, headers?: Record<string, string>): FetchImpl {
  return async () => ({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
    headers: { get: (name: string) => headers?.[name.toLowerCase()] ?? null },
  });
}

const adminMe: MeResponse = {
  email: "admin@example.com",
  roles: ["Admin"],
  permissions: [...PERMISSION_CATALOG],
};

describe("auth api client", () => {
  test("login posts credentials and returns the token shape", async () => {
    let seen: { url: string; init?: RequestInit } = { url: "" };
    const fetchImpl: FetchImpl = async (url, init) => {
      seen = { url, init };
      return {
        ok: true,
        status: 200,
        json: async () => ({ token: "t", mustChangePassword: false }),
      };
    };
    const client = createApiClient(fetchImpl);

    const { result } = await client.login({ email: "a@x.com", password: "s3cret" });

    expect(seen.url).toBe("/api/v1/auth/login");
    expect(JSON.parse(String(seen.init?.body))).toEqual({ email: "a@x.com", password: "s3cret" });
    expect(result).toEqual({ ok: true, value: { token: "t", mustChangePassword: false } });
  });

  test("login and setup never attach a bearer token", async () => {
    const seen: Array<RequestInit | undefined> = [];
    const fetchImpl: FetchImpl = async (_url, init) => {
      seen.push(init);
      return {
        ok: true,
        status: 200,
        json: async () => ({ token: "t", mustChangePassword: true }),
      };
    };
    const client = createApiClient(fetchImpl, { getToken: () => "stored" });

    await client.login({ email: "a@x.com", password: "s3cret" });
    await client.setupPassword({ email: "a@x.com", currentSecret: "old", newPassword: "new" });

    for (const init of seen) {
      expect(
        (init?.headers as Record<string, string> | undefined)?.["authorization"],
      ).toBeUndefined();
    }
  });

  test("401 login maps the stable code", async () => {
    const client = createApiClient(
      stubJson(401, {
        status: 401,
        title: "Invalid credentials.",
        detail: "",
        code: "INVALID_CREDENTIALS",
      }),
    );

    const { result } = await client.login({ email: "a@x.com", password: "wrong" });

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.code).toBe("INVALID_CREDENTIALS");
    }
  });

  test("fetchMe attaches the bearer token", async () => {
    let header: string | undefined;
    const fetchImpl: FetchImpl = async (_url, init) => {
      header = (init?.headers as Record<string, string>)["authorization"];
      return { ok: true, status: 200, json: async () => adminMe };
    };
    const client = createApiClient(fetchImpl, { getToken: () => "tok-1" });

    const { result } = await client.fetchMe();

    expect(header).toBe("Bearer tok-1");
    expect(result).toEqual({ ok: true, value: adminMe });
  });

  test("createUser sends the idempotency key", async () => {
    let header: string | undefined;
    const fetchImpl: FetchImpl = async (_url, init) => {
      header = (init?.headers as Record<string, string>)["idempotency-key"];
      return { ok: true, status: 201, json: async () => ({ id: "u1" }) };
    };
    const client = createApiClient(fetchImpl, { getToken: () => "tok-1" });

    await client.createUser({ email: "n@x.com", temporaryPassword: "Temp-1!!" }, "key-9");

    expect(header).toBe("key-9");
  });

  test("permission grant and revoke hit the catalog paths", async () => {
    const seen: string[] = [];
    const fetchImpl: FetchImpl = async (url, init) => {
      seen.push(`${init?.method} ${url}`);
      return { ok: true, status: 200, json: async () => ({}) };
    };
    const client = createApiClient(fetchImpl, { getToken: () => "tok-1" });

    await client.grantPermission("Operator", "runs.read");
    await client.revokePermission("Operator", "runs.read");

    expect(seen).toEqual([
      "POST /api/v1/roles/Operator/permissions",
      "DELETE /api/v1/roles/Operator/permissions/runs.read",
    ]);
  });

  test("quotas list and update use the scope paths", async () => {
    const seen: string[] = [];
    const fetchImpl: FetchImpl = async (url, init) => {
      seen.push(`${init?.method} ${url}`);
      return url === "/api/v1/quotas"
        ? { ok: true, status: 200, json: async () => ({ quotas: [] }) }
        : {
            ok: true,
            status: 200,
            json: async () => ({ scope: "Api", limit: 60, window: "00:01:00" }),
          };
    };
    const client = createApiClient(fetchImpl, { getToken: () => "tok-1" });

    const listed = await client.listQuotas();
    const updated = await client.updateQuota("Api", { limit: 60, window: "00:01:00" });

    expect(seen).toEqual(["GET /api/v1/quotas", "PUT /api/v1/quotas/Api"]);
    expect(listed.result).toEqual({ ok: true, value: { quotas: [] } });
    expect(updated.result).toEqual({
      ok: true,
      value: { scope: "Api", limit: 60, window: "00:01:00" },
    });
  });

  test("429 answers carry the Retry-After backoff on the problem", async () => {
    const client = createApiClient(
      stubJson(
        429,
        { status: 429, title: "Too many requests.", detail: "", code: "RATE_LIMITED" },
        { "retry-after": "30" },
      ),
    );

    const { result } = await client.listUsers();

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.problem.code).toBe("RATE_LIMITED");
      expect(result.problem.retryAfter).toBe(30);
    }
  });
});

describe("auth store", () => {
  test("token round-trips through storage and clears on sign out", () => {
    expect(loadToken()).toBeNull();
    saveToken("tok-1");
    expect(loadToken()).toBe("tok-1");
    clearToken();
    expect(loadToken()).toBeNull();
  });

  test("nav visibility follows users and roles permissions", () => {
    expect(hasAnyPermission(adminMe, "users.read", "users.write")).toBe(true);
    expect(hasAnyPermission(adminMe, "roles.read", "roles.write")).toBe(true);
    const viewer: MeResponse = { email: "v", roles: ["Viewer"], permissions: ["profiles.read"] };
    expect(hasAnyPermission(viewer, "users.read", "users.write")).toBe(false);
    expect(hasAnyPermission(viewer, "roles.read", "roles.write")).toBe(false);
    expect(hasAnyPermission(viewer, "profiles.read", "users.read")).toBe(true);
  });

  test("only 401 token codes count as a dead token", () => {
    expect(isTokenFailure({ status: 401, code: "UNAUTHENTICATED" })).toBe(true);
    expect(isTokenFailure({ status: 401, code: "INVALID_CREDENTIALS" })).toBe(false);
    expect(isTokenFailure({ status: 403, code: "ACCESS_DENIED" })).toBe(false);
  });

  test("router parses the admin stops", () => {
    expect(parseRoute("#/users")).toBe("users");
    expect(parseRoute("#/roles")).toBe("roles");
    expect(parseRoute("#/quotas")).toBe("quotas");
  });
});

function authBackend() {
  const fetchImpl: FetchImpl = async (url: string, init?: RequestInit) => {
    const reply = (status: number, body: unknown) => ({
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    });
    if (url === "/healthz") {
      return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
    }
    if (url === "/api/v1/auth/login" && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as { email: string; password: string };
      if (body.password !== "right") {
        return reply(401, {
          status: 401,
          title: "Invalid credentials.",
          detail: "",
          code: "INVALID_CREDENTIALS",
        });
      }
      return reply(200, { token: "tok-1", mustChangePassword: false });
    }
    if (url === "/api/v1/auth/setup-password") {
      return reply(200, { token: "tok-2", mustChangePassword: false });
    }
    if (url === "/api/v1/auth/change-password" && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as { currentPassword: string };
      if (body.currentPassword !== "right") {
        return reply(401, {
          status: 401,
          title: "Invalid credentials.",
          detail: "",
          code: "INVALID_CREDENTIALS",
        });
      }
      return reply(204, {});
    }
    if (url === "/api/v1/auth/me") {
      const auth = (init?.headers as Record<string, string> | undefined)?.["authorization"];
      if (auth === "Bearer tok-1" || auth === "Bearer tok-2") {
        return reply(200, adminMe);
      }
      return reply(401, {
        status: 401,
        title: "Unauthenticated.",
        detail: "",
        code: "UNAUTHENTICATED",
      });
    }
    if (url === "/api/v1/users") {
      return reply(200, {
        users: [
          { id: "u1", email: "admin@example.com", mustChangePassword: false, roles: ["Admin"] },
        ],
      });
    }
    if (url === "/api/v1/roles") {
      return reply(200, {
        roles: [{ id: "r1", name: "Admin", permissions: ["users.read"] }],
      });
    }
    if (url === "/api/v1/quotas") {
      return reply(200, { quotas: [{ scope: "Api", limit: 60, window: "00:01:00" }] });
    }
    return reply(200, { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" });
  };
  // The shell reads the token live from tab storage, exactly like production.
  return createApiClient(fetchImpl, { getToken: () => loadToken() });
}

describe("auth flow", () => {
  test("signed out renders sign in without the tab rail", () => {
    const queries = render(<App client={authBackend()} initialRoute="overview" />);

    expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined();
    expect(queries.queryByRole("navigation", { name: "Deck pages" })).toBeNull();
  });

  test("wrong password shows the stable code, right password signs in", async () => {
    const queries = render(<App client={authBackend()} initialRoute="overview" />);
    await waitFor(() => expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined());

    fireEvent.change(queries.getByLabelText("Email"), { target: { value: "a@x.com" } });
    fireEvent.change(queries.getByLabelText("Password"), { target: { value: "wrong" } });
    fireEvent.click(queries.getByRole("button", { name: "SIGN IN" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("INVALID_CREDENTIALS"),
    );

    fireEvent.change(queries.getByLabelText("Password"), { target: { value: "right" } });
    fireEvent.click(queries.getByRole("button", { name: "SIGN IN" }));

    await waitFor(() => expect(queries.container.textContent).toContain("admin@example.com"));
    expect(queries.getByRole("button", { name: "SIGN OUT" })).toBeDefined();
    expect(loadToken()).toBe("tok-1");
  });

  test("wrong password then setup-password finishes signed in", async () => {
    const queries = render(<App client={authBackend()} initialRoute="overview" />);
    await waitFor(() => expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined());

    fireEvent.change(queries.getByLabelText("Email"), { target: { value: "seed@example.com" } });
    fireEvent.change(queries.getByLabelText("Password"), { target: { value: "temp" } });
    fireEvent.click(queries.getByRole("button", { name: "SIGN IN" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("INVALID_CREDENTIALS"),
    );

    fireEvent.click(queries.getByRole("button", { name: "First login / setup password" }));

    await waitFor(() => expect(queries.getByRole("form", { name: "Set password" })).toBeDefined());

    fireEvent.change(queries.getByLabelText("New password"), { target: { value: "New-1!!" } });
    fireEvent.change(queries.getByLabelText("Current secret"), { target: { value: "temp" } });
    fireEvent.click(queries.getByRole("button", { name: "SET PASSWORD" }));

    await waitFor(() => expect(queries.container.textContent).toContain("admin@example.com"));
    expect(loadToken()).toBe("tok-2");
  });

  test("viewer deep links to a hidden stop get access denied", async () => {
    const queries = render(
      <App
        client={authBackend()}
        initialRoute="users"
        initialSession={{
          me: { email: "v@example.com", roles: ["Viewer"], permissions: ["profiles.read"] },
        }}
      />,
    );
    await waitFor(() => expect(queries.container.textContent).toContain("ACCESS_DENIED"));
  });

  test("users page lists accounts with their roles", async () => {
    const queries = render(
      <App client={authBackend()} initialRoute="users" initialSession={{ me: adminMe }} />,
    );

    await waitFor(() => expect(queries.getByRole("table", { name: "Users" })).toBeDefined());
    expect(queries.getByRole("table", { name: "Users" }).textContent).toContain(
      "admin@example.com",
    );
    expect(queries.container.textContent).toContain("CHANGE PASSWORD");
  });

  test("roles page shows the catalog with grant states", async () => {
    const queries = render(
      <App client={authBackend()} initialRoute="roles" initialSession={{ me: adminMe }} />,
    );

    await waitFor(() =>
      expect(queries.getByRole("table", { name: "Admin permissions" })).toBeDefined(),
    );
    const table = queries.getByRole("table", { name: "Admin permissions" });
    expect(table.textContent).toContain("users.read");
    expect(table.textContent).toContain("sources.read");
  });

  test("quotas page lists scopes with their windows", async () => {
    const queries = render(
      <App client={authBackend()} initialRoute="quotas" initialSession={{ me: adminMe }} />,
    );

    await waitFor(() => expect(queries.getByRole("table", { name: "Usage quotas" })).toBeDefined());
    const table = queries.getByRole("table", { name: "Usage quotas" });
    expect(table.textContent).toContain("Api");
    expect(table.textContent).toContain("00:01:00");
  });

  test("wrong current password on change-password keeps the session", async () => {
    saveToken("tok-1");
    const queries = render(
      <App client={authBackend()} initialRoute="users" initialSession={{ me: adminMe }} />,
    );

    await waitFor(() =>
      expect(queries.getByRole("form", { name: "Change my password" })).toBeDefined(),
    );

    fireEvent.change(queries.getByLabelText("Current password"), { target: { value: "wrong" } });
    fireEvent.change(queries.getByLabelText("New password"), { target: { value: "New-1!!" } });
    fireEvent.click(queries.getByRole("button", { name: "CHANGE PASSWORD" }));

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("INVALID_CREDENTIALS"),
    );
    expect(queries.container.textContent).toContain("admin@example.com");
    expect(queries.getByRole("button", { name: "SIGN OUT" })).toBeDefined();
    expect(loadToken()).toBe("tok-1");
  });
});
