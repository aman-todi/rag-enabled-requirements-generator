export interface StreamHandlers {
  onChunk: (text: string) => void;
  onError: (message: string) => void;
  onDone: () => void;
}

interface StreamEventPayload {
  data: string;
}

/**
 * Posts a prompt to /api/requirements/generate and relays the Server-Sent Events response
 * (event: message | error | done) to the caller as it streams in. Same-origin fetch is used
 * deliberately (see vite.config.ts dev proxy) so this works identically in dev and once deployed
 * behind Azure App Service / Entra ID auth.
 */
export async function generateRequirements(
  prompt: string,
  handlers: StreamHandlers,
  signal?: AbortSignal,
): Promise<void> {
  const response = await fetch("/api/requirements/generate", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ prompt }),
    signal,
  });

  if (!response.ok || !response.body) {
    handlers.onError(`Request failed (HTTP ${response.status}).`);
    return;
  }

  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = "";

  while (true) {
    const { value, done } = await reader.read();
    if (done) break;

    buffer += decoder.decode(value, { stream: true });

    const events = buffer.split("\n\n");
    buffer = events.pop() ?? "";

    for (const rawEvent of events) {
      handleEvent(rawEvent, handlers);
    }
  }

  handlers.onDone();
}

function handleEvent(rawEvent: string, handlers: StreamHandlers): void {
  const eventName = rawEvent.match(/^event: (.+)$/m)?.[1] ?? "message";
  const dataLine = rawEvent.match(/^data: (.+)$/m)?.[1];
  if (!dataLine) return;

  let payload: StreamEventPayload;
  try {
    payload = JSON.parse(dataLine) as StreamEventPayload;
  } catch {
    return;
  }

  switch (eventName) {
    case "message":
      handlers.onChunk(payload.data);
      break;
    case "error":
      handlers.onError(payload.data);
      break;
    case "done":
      handlers.onDone();
      break;
  }
}

export interface CurrentUser {
  authenticated: boolean;
  name: string | null;
  id: string | null;
  identityProvider: string | null;
}

/** Reads the signed-in identity that Azure App Service authentication (Easy Auth) injects. */
export async function fetchCurrentUser(): Promise<CurrentUser> {
  const response = await fetch("/api/user/me");
  if (!response.ok) {
    return { authenticated: false, name: null, id: null, identityProvider: null };
  }
  return (await response.json()) as CurrentUser;
}
