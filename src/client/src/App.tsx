import { useEffect, useRef, useState } from "react";
import AppHeader from "./components/AppHeader";
import PromptComposer from "./components/PromptComposer";
import RequirementsOutput from "./components/RequirementsOutput";
import { fetchCurrentUser, generateRequirements, type CurrentUser } from "./api/requirementsApi";

export default function App() {
  const [user, setUser] = useState<CurrentUser | null>(null);
  const [prompt, setPrompt] = useState("");
  const [output, setOutput] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isStreaming, setIsStreaming] = useState(false);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    fetchCurrentUser().then(setUser).catch(() => setUser(null));
  }, []);

  async function handleSubmit() {
    setOutput("");
    setError(null);
    setIsStreaming(true);

    const controller = new AbortController();
    abortRef.current = controller;

    try {
      await generateRequirements(
        prompt,
        {
          onChunk: (chunk) => setOutput((prev) => prev + chunk),
          onError: (message) => setError(message),
          onDone: () => setIsStreaming(false),
        },
        controller.signal,
      );
    } catch (err) {
      if ((err as DOMException).name !== "AbortError") {
        setError("Something went wrong while contacting the requirements generator.");
      }
    } finally {
      setIsStreaming(false);
    }
  }

  function handleCancel() {
    abortRef.current?.abort();
    setIsStreaming(false);
  }

  return (
    <div className="app">
      <AppHeader user={user} />
      <main className="app__main">
        <PromptComposer
          prompt={prompt}
          onPromptChange={setPrompt}
          onSubmit={handleSubmit}
          onCancel={handleCancel}
          isStreaming={isStreaming}
        />
        <RequirementsOutput text={output} isStreaming={isStreaming} error={error} />
      </main>
      <footer className="app__footer">
        Requirements are generated based on an internal Simulink requirements-writing guide via a
        retrieval-augmented Microsoft Foundry model deployment. Review before use.
      </footer>
    </div>
  );
}
