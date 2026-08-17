import { type FormEvent } from "react";

interface PromptComposerProps {
  prompt: string;
  onPromptChange: (value: string) => void;
  onSubmit: () => void;
  onCancel: () => void;
  isStreaming: boolean;
}

const PLACEHOLDER =
  'e.g. "Write complete, testable requirements for an RC filter module"';

export default function PromptComposer({
  prompt,
  onPromptChange,
  onSubmit,
  onCancel,
  isStreaming,
}: PromptComposerProps) {
  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!isStreaming && prompt.trim().length > 0) {
      onSubmit();
    }
  }

  return (
    <form className="composer" onSubmit={handleSubmit}>
      <label htmlFor="prompt" className="composer__label">
        Describe the Simulink component
      </label>
      <textarea
        id="prompt"
        className="composer__textarea"
        placeholder={PLACEHOLDER}
        value={prompt}
        rows={4}
        disabled={isStreaming}
        onChange={(event) => onPromptChange(event.target.value)}
      />
      <div className="composer__actions">
        {isStreaming ? (
          <button type="button" className="btn btn--secondary" onClick={onCancel}>
            Stop
          </button>
        ) : (
          <button type="submit" className="btn btn--primary" disabled={prompt.trim().length === 0}>
            Generate requirements
          </button>
        )}
      </div>
    </form>
  );
}
