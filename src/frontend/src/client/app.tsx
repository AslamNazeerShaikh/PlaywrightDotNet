import { useState } from "react";

import {
  type ApiClient,
  type ApiProblem,
  createApiClient,
  type HealthPayload,
  type SearchProfilePayload,
} from "./lib/api.ts";

export function App({ client = createApiClient() }: { client?: ApiClient }) {
  return (
    <main className="cockpit">
      <header>
        <h1>JobScrape Cockpit</h1>
        <p>Walking skeleton: engine health plus search-profile lookup.</p>
      </header>
      <HealthCard client={client} />
      <ProfileLookup client={client} />
    </main>
  );
}

function HealthCard({ client }: { client: ApiClient }) {
  const [health, setHealth] = useState<HealthPayload | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [loading, setLoading] = useState(false);

  async function check() {
    setLoading(true);
    setProblem(null);
    const result = await client.fetchHealth();
    if (result.ok) {
      setHealth(result.value);
    } else {
      setHealth(null);
      setProblem(result.problem);
    }
    setLoading(false);
  }

  return (
    <section aria-label="Engine health">
      <h2>Engine health</h2>
      <button type="button" onClick={check} disabled={loading}>
        {loading ? "Checking…" : "Check health"}
      </button>
      {health !== null && (
        <p role="status">
          {health.status} · {health.timeUtc}
        </p>
      )}
      {problem !== null && <ProblemNotice problem={problem} />}
    </section>
  );
}

function ProfileLookup({ client }: { client: ApiClient }) {
  const [profile, setProfile] = useState<SearchProfilePayload | null>(null);
  const [problem, setProblem] = useState<ApiProblem | null>(null);
  const [loading, setLoading] = useState(false);

  async function lookup(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    // Uncontrolled input read via FormData: no per-keystroke React state,
    // and the suite can drive the DOM value without a synthetic onChange.
    const raw = String(new FormData(event.currentTarget).get("profileId") ?? "").trim();
    if (raw === "") {
      setProfile(null);
      setProblem({
        status: 400,
        title: "Profile id required.",
        detail: "Enter a search-profile id to look up.",
        code: "VALIDATION_FAILED",
      });
      return;
    }
    setLoading(true);
    setProblem(null);
    const result = await client.fetchProfile(raw);
    if (result.ok) {
      setProfile(result.value);
    } else {
      setProfile(null);
      setProblem(result.problem);
    }
    setLoading(false);
  }

  return (
    <section aria-label="Search profile lookup">
      <h2>Search profile lookup</h2>
      <form onSubmit={lookup}>
        <label>
          Profile id
          <input
            name="profileId"
            aria-label="Profile id"
            placeholder="00000000-0000-0000-0000-000000000000"
          />
        </label>
        <button type="submit" disabled={loading}>
          {loading ? "Looking up…" : "Look up"}
        </button>
      </form>
      {profile !== null && (
        <p role="status">
          {profile.name} · {profile.sourceKey}
        </p>
      )}
      {problem !== null && <ProblemNotice problem={problem} />}
    </section>
  );
}

function ProblemNotice({ problem }: { problem: ApiProblem }) {
  return (
    <p role="alert">
      {problem.code}: {problem.title}
      {problem.detail !== "" ? ` — ${problem.detail}` : ""}
    </p>
  );
}
