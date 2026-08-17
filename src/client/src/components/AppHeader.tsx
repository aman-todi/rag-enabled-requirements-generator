import type { CurrentUser } from "../api/requirementsApi";

interface AppHeaderProps {
  user: CurrentUser | null;
}

export default function AppHeader({ user }: AppHeaderProps) {
  return (
    <header className="app-header">
      <div className="app-header__title">
        <span className="app-header__badge">RAG</span>
        <h1>Simulink Requirements Generator</h1>
      </div>
      <div className="app-header__user">
        {user?.authenticated ? (
          <span>
            Signed in as <strong>{user.name}</strong>
          </span>
        ) : (
          <span className="app-header__user--anonymous">Not signed in</span>
        )}
      </div>
    </header>
  );
}
