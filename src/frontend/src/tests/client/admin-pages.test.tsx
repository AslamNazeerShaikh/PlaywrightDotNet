import { afterAll, afterEach, beforeAll, describe, expect, test } from "bun:test";

import { GlobalRegistrator } from "@happy-dom/global-registrator";
import { cleanup, fireEvent, render, waitFor } from "@testing-library/react";

import type { ApiClient, MeResponse } from "../../client/lib/api.ts";
import { PERMISSION_CATALOG } from "../../client/lib/api.ts";
import { QuotasPage } from "../../client/pages/quotas.tsx";
import { RolesPage } from "../../client/pages/roles.tsx";
import { UsersPage } from "../../client/pages/users.tsx";

beforeAll(() => {
  GlobalRegistrator.register();
});

afterAll(async () => {
  await new Promise((resolve) => setTimeout(resolve, 100));
  await GlobalRegistrator.unregister();
});

afterEach(() => {
  cleanup();
});

const adminMe: MeResponse = {
  email: "admin@example.com",
  roles: ["Admin"],
  permissions: [...PERMISSION_CATALOG],
};

const oneUser = {
  id: "u1",
  email: "a@x.com",
  mustChangePassword: false,
  roles: ["Admin"],
};

function usersClient(calls: { getUser: string[] }): ApiClient {
  return {
    listUsers: async () => ({
      result: { ok: true, value: { users: [oneUser] } },
      ms: 1,
      at: new Date().toISOString(),
    }),
    listRoles: async () => ({
      result: {
        ok: true,
        value: {
          roles: [
            { id: "r1", name: "Admin", permissions: [] },
            { id: "r2", name: "Operator", permissions: [] },
          ],
        },
      },
      ms: 1,
      at: new Date().toISOString(),
    }),
    getUser: async (id: string) => {
      calls.getUser.push(id);
      return { result: { ok: true, value: oneUser }, ms: 1, at: new Date().toISOString() };
    },
  } as unknown as ApiClient;
}

describe("users detail", () => {
  test("re-fetches the selected user when the client prop is hot-swapped", async () => {
    const first = { getUser: [] as string[] };
    const second = { getUser: [] as string[] };
    const clientA = usersClient(first);
    const clientB = usersClient(second);
    const queries = render(
      <UsersPage client={clientA} me={adminMe} onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByRole("button", { name: "a@x.com" })).toBeTruthy());
    fireEvent.click(queries.getByRole("button", { name: "a@x.com" }));
    await waitFor(() => expect(first.getUser).toEqual(["u1"]));

    queries.rerender(<UsersPage client={clientB} me={adminMe} onUnauthorized={() => undefined} />);

    await waitFor(() => expect(second.getUser).toEqual(["u1"]));
  });
});

function rolesClient(): {
  client: ApiClient;
  state: Array<{ id: string; name: string; permissions: string[] }>;
} {
  const state = [
    { id: "r1", name: "Admin", permissions: [] as string[] },
    { id: "r2", name: "Operator", permissions: [] as string[] },
  ];
  const client = {
    listRoles: async () => ({
      result: { ok: true, value: { roles: state.map((r) => ({ ...r })) } },
      ms: 1,
      at: new Date().toISOString(),
    }),
    createRole: async (input: { name: string }) => {
      const created = { id: `r${state.length + 1}`, name: input.name, permissions: [] };
      state.push(created);
      return { result: { ok: true, value: { ...created } }, ms: 1, at: new Date().toISOString() };
    },
    grantPermission: async () => ({
      result: { ok: true, value: {} },
      ms: 1,
      at: new Date().toISOString(),
    }),
    revokePermission: async () => ({
      result: { ok: true, value: {} },
      ms: 1,
      at: new Date().toISOString(),
    }),
  } as unknown as ApiClient;
  return { client, state };
}

describe("roles selection", () => {
  test("keeps the pinned role across a reload", async () => {
    const { client } = rolesClient();
    const queries = render(<RolesPage client={client} canWrite onUnauthorized={() => undefined} />);
    await waitFor(() => expect(queries.getByRole("button", { name: "Operator" })).toBeTruthy());
    fireEvent.click(queries.getByRole("button", { name: "Operator" }));
    await waitFor(() => expect(queries.container.textContent).toContain("Operator permissions"));

    fireEvent.click(queries.getByRole("button", { name: "REFRESH" }));

    await waitFor(() => expect(queries.container.textContent).toContain("Operator permissions"));
  });

  test("re-pins to the first role when the pinned role vanishes from the catalog", async () => {
    const { client, state } = rolesClient();
    const queries = render(<RolesPage client={client} canWrite onUnauthorized={() => undefined} />);
    await waitFor(() => expect(queries.getByRole("button", { name: "Operator" })).toBeTruthy());
    fireEvent.click(queries.getByRole("button", { name: "Operator" }));
    await waitFor(() => expect(queries.container.textContent).toContain("Operator permissions"));

    state.splice(
      state.findIndex((r) => r.name === "Operator"),
      1,
    );
    fireEvent.click(queries.getByRole("button", { name: "REFRESH" }));

    await waitFor(() => expect(queries.container.textContent).toContain("Admin permissions"));
  });

  test("selects the role it created instead of a stale pin", async () => {
    const { client, state } = rolesClient();
    const queries = render(<RolesPage client={client} canWrite onUnauthorized={() => undefined} />);
    await waitFor(() => expect(queries.container.textContent).toContain("2 roles"));

    fireEvent.click(queries.getByRole("button", { name: "CREATE ROLE" }));

    await waitFor(() => expect(state.map((r) => r.name)).toContain("Viewer"));
    await waitFor(() => expect(queries.container.textContent).toContain("Viewer permissions"));
  });
});

function quotasClient(calls: {
  updateQuota: Array<{ scope: string; window: string; limit: number }>;
}): ApiClient {
  let quotas = [{ scope: "Api", limit: 60, window: "00:01:00" }];
  return {
    listQuotas: async () => ({
      result: { ok: true, value: { quotas: quotas.map((q) => ({ ...q })) } },
      ms: 1,
      at: new Date().toISOString(),
    }),
    updateQuota: async (scope: string, input: { limit: number; window: string }) => {
      calls.updateQuota.push({ scope, limit: input.limit, window: input.window });
      quotas = quotas.map((q) => (q.scope === scope ? { ...q, ...input } : q));
      return {
        result: { ok: true, value: { scope, ...input } },
        ms: 1,
        at: new Date().toISOString(),
      };
    },
  } as unknown as ApiClient;
}

describe("quotas window validation", () => {
  test("rejects an invalid window inline without calling the API", async () => {
    const calls = { updateQuota: [] as Array<{ scope: string; window: string; limit: number }> };
    const client = quotasClient(calls);
    const queries = render(
      <QuotasPage client={client} canWrite onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByLabelText("Api window")).toBeTruthy());

    fireEvent.change(queries.getByLabelText("Api window"), { target: { value: "not-a-window" } });
    fireEvent.click(queries.getByRole("button", { name: "SAVE" }));

    await waitFor(() => expect(queries.container.textContent).toContain("not a valid time span"));
    expect(calls.updateQuota).toEqual([]);
  });

  test("accepts a valid window and calls the API", async () => {
    const calls = { updateQuota: [] as Array<{ scope: string; window: string; limit: number }> };
    const client = quotasClient(calls);
    const queries = render(
      <QuotasPage client={client} canWrite onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByLabelText("Api window")).toBeTruthy());

    fireEvent.change(queries.getByLabelText("Api window"), { target: { value: "00:05:00" } });
    fireEvent.click(queries.getByRole("button", { name: "SAVE" }));

    await waitFor(() =>
      expect(calls.updateQuota).toEqual([{ scope: "Api", limit: 60, window: "00:05:00" }]),
    );
  });

  test("accepts the backend's days-qualified form and rejects out-of-range parts", async () => {
    const calls = { updateQuota: [] as Array<{ scope: string; window: string; limit: number }> };
    const client = quotasClient(calls);
    const queries = render(
      <QuotasPage client={client} canWrite onUnauthorized={() => undefined} />,
    );
    await waitFor(() => expect(queries.getByLabelText("Api window")).toBeTruthy());

    for (const invalid of [
      "1.25:00:00",
      "00:60:00",
      "00:00:60",
      "00:01:00.12345678",
      "1:2:3:4:5",
    ]) {
      fireEvent.change(queries.getByLabelText("Api window"), { target: { value: invalid } });
      fireEvent.click(queries.getByRole("button", { name: "SAVE" }));
      await waitFor(() => expect(queries.container.textContent).toContain("not a valid time span"));
    }
    expect(calls.updateQuota).toEqual([]);

    fireEvent.change(queries.getByLabelText("Api window"), { target: { value: "1.00:00:00" } });
    fireEvent.click(queries.getByRole("button", { name: "SAVE" }));
    await waitFor(() =>
      expect(calls.updateQuota).toEqual([{ scope: "Api", limit: 60, window: "1.00:00:00" }]),
    );
  });
});
