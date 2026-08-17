interface RequirementsOutputProps {
  text: string;
  isStreaming: boolean;
  error: string | null;
}

export default function RequirementsOutput({ text, isStreaming, error }: RequirementsOutputProps) {
  async function handleCopy() {
    await navigator.clipboard.writeText(text);
  }

  if (!text && !isStreaming && !error) {
    return (
      <div className="output output--empty">
        Generated requirements will appear here, formatted per the internal requirements-writing
        guide.
      </div>
    );
  }

  return (
    <div className="output">
      <div className="output__toolbar">
        {isStreaming && <span className="output__status">Generating…</span>}
        {!isStreaming && text && (
          <button type="button" className="btn btn--ghost" onClick={handleCopy}>
            Copy
          </button>
        )}
      </div>
      {error && <div className="output__error">{error}</div>}
      {text && <pre className="output__text">{text}</pre>}
    </div>
  );
}
