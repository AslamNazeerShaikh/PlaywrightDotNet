import { CodeTag, Field, Module } from "../components/modules.tsx";
import { Scramble } from "../components/scramble.tsx";
import type { ApiProblem } from "../lib/api.ts";
import { useRenderLog } from "../lib/debug.ts";
import { readText } from "../lib/forms.ts";

/**
 * Sign-in and first-login setup. Both forms are uncontrolled (read from FormData on submit,
 * happy-dom-safe); the server is the only validator, so every rule surfaces here verbatim keyed by
 * its JSON field name. Passwords are never stored: they travel one request and the form resets.
 */

/** Looks up field errors tolerating the API's PascalCase keys ("Email") and camelCase names. */
export function fieldErrorsFor(
  errors: Record<string, string[]> | undefined,
  name: string,
): Record<string, string[]> {
  if (errors === undefined) {
    return {};
  }
  const cased = name.charAt(0).toUpperCase() + name.slice(1);
  const messages = errors[name] ?? errors[cased] ?? errors[name.toLowerCase()] ?? [];
  return messages.length === 0 ? {} : { [name]: messages };
}

/** One alert row for an auth/admin failure: stable code first, prose second, backoff when 429. */
export function AuthProblem({ problem }: { problem: ApiProblem }) {
  const backoff =
    problem.status === 429 && problem.retryAfter !== undefined && problem.retryAfter !== null
      ? ` Back off ${problem.retryAfter}s, then retry.`
      : "";
  return (
    <p className="alert" role="alert">
      <CodeTag code={problem.code} tone="bad" /> {problem.title}
      {problem.detail !== "" ? ` — ${problem.detail}` : ""}
      {backoff}
    </p>
  );
}

export function LoginPage({
  initialEmail,
  busy,
  problem,
  onLogin,
  onFirstLogin,
}: {
  initialEmail: string;
  busy: boolean;
  problem: ApiProblem | null;
  onLogin: (input: { email: string; password: string }) => Promise<boolean>;
  onFirstLogin?: () => void;
}) {
  useRenderLog("LoginPage");

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const ok = await onLogin({
      email: readText(data, "email"),
      password: readText(data, "password"),
    });
    if (ok) {
      form.reset();
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // SIGN IN" />
        </h1>
        <p>One operator, one token. The BFF forwards the bearer; the deck never logs it.</p>
      </div>

      <Module
        code="AUTH-01"
        title="Sign in"
        span="span-4"
        note="POST /api/v1/auth/login. A 401 means the email or secret is wrong."
      >
        <form onSubmit={submit} aria-label="Sign in" noValidate>
          <Field label="EMAIL" name="email" errors={fieldErrorsFor(problem?.errors, "email")}>
            <input
              className="input"
              name="email"
              aria-label="Email"
              autoComplete="username"
              defaultValue={initialEmail}
            />
          </Field>
          <Field
            label="PASSWORD"
            name="password"
            errors={fieldErrorsFor(problem?.errors, "password")}
          >
            <input
              className="input"
              type="password"
              name="password"
              aria-label="Password"
              autoComplete="current-password"
            />
          </Field>
          <button type="submit" className="btn" disabled={busy}>
            {busy ? "SIGNING IN…" : "SIGN IN"}
          </button>
        </form>
        {problem !== null && <AuthProblem problem={problem} />}
      </Module>

      <Module
        code="AUTH-02"
        title="First login / setup password"
        span="span-4"
        note="Seeded accounts finish at setup-password before they can sign in."
      >
        <p className="status-line">
          Pending accounts trade their seeded secret for an operator password here.
        </p>
        <button type="button" className="btn btn-ghost btn-sm" onClick={() => onFirstLogin?.()}>
          First login / setup password
        </button>
      </Module>
    </>
  );
}

export function SetupPasswordPage({
  initialEmail,
  busy,
  problem,
  onSetup,
}: {
  initialEmail: string;
  busy: boolean;
  problem: ApiProblem | null;
  onSetup: (input: {
    email: string;
    currentSecret: string;
    newPassword: string;
  }) => Promise<boolean>;
}) {
  useRenderLog("SetupPasswordPage");

  async function submit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const ok = await onSetup({
      email: readText(data, "email"),
      currentSecret: readText(data, "currentSecret"),
      newPassword: readText(data, "newPassword"),
    });
    if (ok) {
      form.reset();
    }
  }

  return (
    <>
      <div className="page-head">
        <h1>
          <Scramble text="OPS-DECK // FIRST LOGIN" />
        </h1>
        <p>
          Trade the seeded secret for an operator password. One request, then the deck signs in.
        </p>
      </div>

      <Module
        code="AUTH-03"
        title="Set password"
        span="span-4"
        note="POST /api/v1/auth/setup-password. A 409 PASSWORD_SETUP_COMPLETE means this already ran."
      >
        <form onSubmit={submit} aria-label="Set password" noValidate>
          <Field label="EMAIL" name="email" errors={fieldErrorsFor(problem?.errors, "email")}>
            <input
              className="input"
              name="email"
              aria-label="Email"
              autoComplete="username"
              defaultValue={initialEmail}
            />
          </Field>
          <Field
            label="CURRENT SECRET"
            name="currentSecret"
            errors={fieldErrorsFor(problem?.errors, "currentSecret")}
          >
            <input
              className="input"
              type="password"
              name="currentSecret"
              aria-label="Current secret"
              autoComplete="current-password"
            />
          </Field>
          <Field
            label="NEW PASSWORD"
            name="newPassword"
            errors={fieldErrorsFor(problem?.errors, "newPassword")}
          >
            <input
              className="input"
              type="password"
              name="newPassword"
              aria-label="New password"
              autoComplete="new-password"
            />
          </Field>
          <button type="submit" className="btn" disabled={busy}>
            {busy ? "SAVING…" : "SET PASSWORD"}
          </button>
        </form>
        {problem !== null && <AuthProblem problem={problem} />}
      </Module>
    </>
  );
}
