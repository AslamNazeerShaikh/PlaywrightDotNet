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
import { hasAnyPermission, isTokenFailure } from "../../client/lib/auth.ts";
import { parseRoute } from "../../client/lib/router.ts";
import { AuthProblem } from "../../client/pages/login.tsx";

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
  test("login posts credentials and returns mustChangePassword", async () => {
    let seen: { url: string; init?: RequestInit } = { url: "" };
    const fetchImpl: FetchImpl = async (url, init) => {
      seen = { url, init };
      return {
        ok: true,
        status: 200,
        json: async () => ({ mustChangePassword: false }),
      };
    };
    const client = createApiClient(fetchImpl);

    const { result } = await client.login({ email: "a@x.com", password: "s3cret" });

    expect(seen.url).toBe("/bff/session");
    expect(JSON.parse(String(seen.init?.body))).toEqual({ email: "a@x.com", password: "s3cret" });
    expect(result).toEqual({ ok: true, value: { mustChangePassword: false } });
  });

  test("login and setup never attach a bearer token", async () => {
    const seen: Array<RequestInit | undefined> = [];
    const fetchImpl: FetchImpl = async (_url, init) => {
      seen.push(init);
      return {
        ok: true,
        status: 200,
        json: async () => ({ mustChangePassword: true }),
      };
    };
    const client = createApiClient(fetchImpl);

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

  test("fetchMe no longer attaches a bearer token", async () => {
    let header: string | undefined;
    const fetchImpl: FetchImpl = async (_url, init) => {
      header = (init?.headers as Record<string, string> | undefined)?.["authorization"];
      return { ok: true, status: 200, json: async () => adminMe };
    };
    const client = createApiClient(fetchImpl);

    const { result } = await client.fetchMe();

    expect(header).toBeUndefined();
    expect(result).toEqual({ ok: true, value: adminMe });
  });

  test("createUser sends the idempotency key", async () => {
    let header: string | undefined;
    const fetchImpl: FetchImpl = async (_url, init) => {
      header = (init?.headers as Record<string, string>)["idempotency-key"];
      return { ok: true, status: 201, json: async () => ({ id: "u1" }) };
    };
    const client = createApiClient(fetchImpl);

    await client.createUser({ email: "n@x.com", temporaryPassword: "Temp-1!!" }, "key-9");

    expect(header).toBe("key-9");
  });

  test("permission grant and revoke hit the catalog paths", async () => {
    const seen: string[] = [];
    const fetchImpl: FetchImpl = async (url, init) => {
      seen.push(`${init?.method} ${url}`);
      return { ok: true, status: 200, json: async () => ({}) };
    };
    const client = createApiClient(fetchImpl);

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
    const client = createApiClient(fetchImpl);

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

  test("Retry-After accepts an HTTP-date and clamps a past date to zero", async () => {
    const body = { status: 429, title: "Too many requests.", detail: "", code: "RATE_LIMITED" };
    const future = new Date(Date.now() + 60_000).toUTCString();
    const futureRes = await createApiClient(
      stubJson(429, body, { "retry-after": future }),
    ).listUsers();
    expect(futureRes.result.ok).toBe(false);
    if (!futureRes.result.ok) {
      expect(futureRes.result.problem.retryAfter).toBeGreaterThanOrEqual(59);
      expect(futureRes.result.problem.retryAfter).toBeLessThanOrEqual(60);
    }

    const pastRes = await createApiClient(
      stubJson(429, body, { "retry-after": "Thu, 01 Jan 1970 00:00:00 GMT" }),
    ).listUsers();
    expect(pastRes.result.ok).toBe(false);
    if (!pastRes.result.ok) {
      expect(pastRes.result.problem.retryAfter).toBe(0);
    }
  });

  test("Retry-After clamps negative delta-seconds to zero and drops garbage", async () => {
    const body = { status: 429, title: "Too many requests.", detail: "", code: "RATE_LIMITED" };
    const negative = await createApiClient(
      stubJson(429, body, { "retry-after": "-5" }),
    ).listUsers();
    expect(negative.result.ok).toBe(false);
    if (!negative.result.ok) {
      expect(negative.result.problem.retryAfter).toBe(0);
    }

    const garbage = await createApiClient(
      stubJson(429, body, { "retry-after": "banana" }),
    ).listUsers();
    expect(garbage.result.ok).toBe(false);
    if (!garbage.result.ok) {
      expect(garbage.result.problem.retryAfter).toBeUndefined();
    }
  });
});

describe("auth store", () => {
  test("auth store helpers remain available", () => {
    expect(hasAnyPermission(adminMe, "users.read")).toBe(true);
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

function authBackend(seededRoles: string[] = ["Admin"], capture?: { posts: unknown[] }) {
  let authed = false;
  const fetchImpl: FetchImpl = async (url: string, init?: RequestInit) => {
    const reply = (status: number, body: unknown) => ({
      ok: status >= 200 && status < 300,
      status,
      json: async () => body,
    });
    if (url === "/healthz") {
      return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
    }
    if (url === "/bff/session" && init?.method === "POST") {
      const body = JSON.parse(String(init.body)) as { email: string; password: string };
      if (body.password !== "right") {
        return reply(401, {
          status: 401,
          title: "Invalid credentials.",
          detail: "",
          code: "INVALID_CREDENTIALS",
        });
      }
      authed = true;
      return reply(200, { mustChangePassword: false });
    }
    if (url === "/bff/session/setup" && init?.method === "POST") {
      authed = true;
      return reply(200, { mustChangePassword: false });
    }
    if (url === "/bff/session" && init?.method === "GET") {
      if (!authed) {
        return reply(200, { authenticated: false });
      }
      return reply(200, adminMe);
    }
    if (url === "/bff/session" && init?.method === "DELETE") {
      authed = false;
      return reply(204, {});
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
      return reply(200, adminMe);
    }
    if (url === "/api/v1/users") {
      return reply(200, {
        users: [
          { id: "u1", email: "admin@example.com", mustChangePassword: false, roles: ["Admin"] },
        ],
      });
    }
    if (url === "/api/v1/roles") {
      if (init?.method === "POST") {
        const body = JSON.parse(String(init.body)) as { name: string };
        capture?.posts.push(body);
        return reply(201, { id: "r-new", name: body.name, permissions: [] });
      }
      return reply(200, {
        roles: seededRoles.map((name) => ({
          id: name.toLowerCase(),
          name,
          permissions: name === "Admin" ? ["users.read"] : [],
        })),
      });
    }
    if (url === "/api/v1/quotas") {
      return reply(200, { quotas: [{ scope: "Api", limit: 60, window: "00:01:00" }] });
    }
    return reply(200, { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" });
  };
  return createApiClient(fetchImpl);
}

describe("auth flow", () => {
  test("signed out renders sign in without the tab rail", async () => {
    const queries = render(<App client={authBackend()} initialRoute="overview" />);

    await waitFor(() => expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined());
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
  });

  test("setup-password 409 lands back on sign-in with a one-time notice", async () => {
    const fetchImpl: FetchImpl = async (url, init) => {
      const reply = (status: number, body: unknown) => ({
        ok: status >= 200 && status < 300,
        status,
        json: async () => body,
      });
      if (url === "/healthz") {
        return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
      }
      if (url === "/bff/session" && init?.method === "GET") {
        return reply(200, { authenticated: false });
      }
      if (url === "/bff/session/setup" && init?.method === "POST") {
        return reply(409, {
          status: 409,
          title: "This account already finished setup. Sign in with its password.",
          detail: "",
          code: "PASSWORD_SETUP_COMPLETE",
        });
      }
      return reply(200, { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" });
    };

    const queries = render(<App client={createApiClient(fetchImpl)} initialRoute="overview" />);
    await waitFor(() => expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "First login / setup password" }));
    await waitFor(() => expect(queries.getByRole("form", { name: "Set password" })).toBeDefined());

    fireEvent.change(queries.getByLabelText("Email"), { target: { value: "seed@example.com" } });
    fireEvent.change(queries.getByLabelText("Current secret"), { target: { value: "temp" } });
    fireEvent.change(queries.getByLabelText("New password"), { target: { value: "New-1!!" } });
    fireEvent.click(queries.getByRole("button", { name: "SET PASSWORD" }));

    await waitFor(() => expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined());
    expect(queries.queryByRole("form", { name: "Set password" })).toBeNull();
    expect(queries.getByRole("alert").textContent).toContain("PASSWORD_SETUP_COMPLETE");
    expect(sessionStorage.getItem("jobscrape.setupEmail")).toBeNull();
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
  });

  test("roles dropdown with zero seeded roles submits Operator", async () => {
    const capture = { posts: [] as unknown[] };
    const queries = render(
      <App
        client={authBackend([], capture)}
        initialRoute="roles"
        initialSession={{ me: adminMe }}
      />,
    );

    await waitFor(() => expect(queries.getByLabelText("Name")).toBeDefined());
    fireEvent.change(queries.getByLabelText("Name"), { target: { value: "Operator" } });
    fireEvent.click(queries.getByRole("button", { name: "CREATE ROLE" }));

    await waitFor(() => expect(capture.posts).toHaveLength(1));
    expect(capture.posts[0]).toEqual({ name: "Operator" });
  });

  test("roles dropdown with one seeded role keeps submit enabled", async () => {
    const queries = render(
      <App client={authBackend(["Admin"])} initialRoute="roles" initialSession={{ me: adminMe }} />,
    );

    await waitFor(() => expect(queries.getByLabelText("Name")).toBeDefined());
    const select = queries.getByLabelText("Name") as HTMLSelectElement;
    expect(select.disabled).toBe(false);
  });

  test("roles dropdown with all roles seeded disables select and button with no post", async () => {
    const capture = { posts: [] as unknown[] };
    const queries = render(
      <App
        client={authBackend(["Admin", "Operator", "Viewer"], capture)}
        initialRoute="roles"
        initialSession={{ me: adminMe }}
      />,
    );

    await waitFor(() => expect(queries.getByLabelText("Name")).toBeDefined());
    const select = queries.getByLabelText("Name") as HTMLSelectElement;
    const button = queries.getByRole("button", { name: "CREATE ROLE" }) as HTMLButtonElement;
    expect(select.disabled).toBe(true);
    expect(button.disabled).toBe(true);
    expect(capture.posts).toHaveLength(0);
  });
});

describe("auth wave 2 proving tests", () => {
  const healthy = { status: "healthy", timeUtc: "2026-10-31T00:00:00Z" };
  const reply = (status: number, body: unknown) => ({
    ok: status >= 200 && status < 300,
    status,
    json: async () => body,
  });

  test("reload with a stored setup email and mustChangePassword shows Set password", async () => {
    sessionStorage.setItem("jobscrape.setupEmail", "seed@example.com");
    const fetchImpl: FetchImpl = async (url, init) => {
      if (url === "/healthz") {
        return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
      }
      if (url === "/bff/session" && init?.method === "GET") {
        return reply(200, {
          email: "seed@example.com",
          roles: ["Viewer"],
          permissions: ["profiles.read"],
          mustChangePassword: true,
        });
      }
      return reply(200, healthy);
    };

    const queries = render(<App client={createApiClient(fetchImpl)} initialRoute="overview" />);

    await waitFor(() => expect(queries.getByRole("form", { name: "Set password" })).toBeDefined());
    expect((queries.getByLabelText("Email") as HTMLInputElement).value).toBe("seed@example.com");
  });

  test("sign out keeps the session until the BFF delete resolves", async () => {
    sessionStorage.setItem("jobscrape.setupEmail", "seed@example.com");
    let deleteCalls = 0;
    let releaseDelete: () => void = () => undefined;
    const deleteGate = new Promise<void>((resolve) => {
      releaseDelete = resolve;
    });
    const fetchImpl: FetchImpl = async (url, init) => {
      if (url === "/healthz") {
        return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
      }
      if (url === "/bff/session" && init?.method === "DELETE") {
        deleteCalls += 1;
        await deleteGate;
        return reply(204, {});
      }
      return reply(200, healthy);
    };

    const queries = render(
      <App
        client={createApiClient(fetchImpl)}
        initialSession={{ me: adminMe }}
        initialRoute="overview"
      />,
    );
    await waitFor(() => expect(queries.getByRole("button", { name: "SIGN OUT" })).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "SIGN OUT" }));

    expect(deleteCalls).toBe(1);
    expect(queries.queryByRole("form", { name: "Sign in" })).toBeNull();
    expect(queries.container.textContent).toContain("admin@example.com");

    releaseDelete();

    await waitFor(() => expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined());
    expect(sessionStorage.getItem("jobscrape.setupEmail")).toBeNull();
    expect(deleteCalls).toBe(1);
  });

  test("sign out still clears the local session when the BFF delete fails", async () => {
    sessionStorage.setItem("jobscrape.setupEmail", "seed@example.com");
    const fetchImpl: FetchImpl = async (url, init) => {
      if (url === "/healthz") {
        return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
      }
      if (url === "/bff/session" && init?.method === "DELETE") {
        return reply(500, { status: 500, title: "Server error.", detail: "", code: "INTERNAL" });
      }
      return reply(200, healthy);
    };

    const queries = render(
      <App
        client={createApiClient(fetchImpl)}
        initialSession={{ me: adminMe }}
        initialRoute="overview"
      />,
    );
    await waitFor(() => expect(queries.getByRole("button", { name: "SIGN OUT" })).toBeDefined());

    fireEvent.click(queries.getByRole("button", { name: "SIGN OUT" }));

    await waitFor(() => expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined());
    expect(sessionStorage.getItem("jobscrape.setupEmail")).toBeNull();
    expect(queries.getByRole("alert").textContent).toContain("Your session ended");
  });

  test("an expired restore renders the session-ended notice on the sign-in form", async () => {
    const fetchImpl: FetchImpl = async (url, init) => {
      if (url === "/healthz") {
        return reply(200, { status: "ok", upstream: "http://127.0.0.1:5207" });
      }
      if (url === "/bff/session" && init?.method === "GET") {
        return reply(200, { authenticated: false, expired: true });
      }
      return reply(200, healthy);
    };

    const queries = render(<App client={createApiClient(fetchImpl)} initialRoute="overview" />);

    await waitFor(() =>
      expect(queries.getByRole("alert").textContent).toContain("Your session ended"),
    );
    expect(queries.getByRole("form", { name: "Sign in" })).toBeDefined();
  });
});

describe("AuthProblem backoff note", () => {
  test("renders the header value for any 429 code", () => {
    const { container } = render(
      <AuthProblem
        problem={{
          status: 429,
          title: "Too many requests.",
          detail: "",
          code: "SOME_NEW_429",
          retryAfter: 45,
        }}
      />,
    );
    expect(container.textContent).toContain("Back off 45s");
  });

  test("renders no backoff note when the status is not 429", () => {
    const { container } = render(
      <AuthProblem
        problem={{
          status: 403,
          title: "Denied.",
          detail: "",
          code: "ACCESS_DENIED",
          retryAfter: 45,
        }}
      />,
    );
    expect(container.textContent).not.toContain("Back off");
  });

  test("renders no backoff note without a Retry-After value", () => {
    const { container } = render(
      <AuthProblem
        problem={{ status: 429, title: "Too many requests.", detail: "", code: "RATE_LIMITED" }}
      />,
    );
    expect(container.textContent).not.toContain("Back off");
  });
});
