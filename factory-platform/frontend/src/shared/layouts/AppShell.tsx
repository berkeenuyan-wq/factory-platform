import { LogOut, Menu } from "lucide-react";
import type { ReactNode } from "react";
import type { FrontendModule } from "../../app/moduleRegistry";
import { useAuth } from "../hooks/useAuth";

type AppShellProps = {
  modules: FrontendModule[];
  activeRoute: string;
  onNavigate: (route: string) => void;
  children: ReactNode;
};

export function AppShell({ modules, activeRoute, onNavigate, children }: AppShellProps) {
  const { user, logout } = useAuth();

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-header">
          <div className="brand-emblem">FP</div>
          <div>
            <strong>Factory Platform</strong>
            <span>v0.1 foundation</span>
          </div>
        </div>
        <nav className="sidebar-nav" aria-label="Main modules">
          {modules.map((module) => {
            const Icon = module.icon;
            return (
              <button
                key={module.key}
                className={module.route === activeRoute ? "active" : ""}
                onClick={() => onNavigate(module.route)}
                type="button"
              >
                <Icon size={18} />
                <span>{module.name}</span>
              </button>
            );
          })}
        </nav>
      </aside>
      <div className="main-frame">
        <header className="topbar">
          <button className="icon-button" type="button" aria-label="Menu">
            <Menu size={20} />
          </button>
          <div className="environment-pill">Internal Factory Network</div>
          <div className="profile-area">
            <div>
              <strong>{user?.displayName}</strong>
              <span>{user?.roles.join(", ")}</span>
            </div>
            <button className="logout-button" onClick={logout} type="button">
              <LogOut size={18} />
              Logout
            </button>
          </div>
        </header>
        <main className="content-area">{children}</main>
      </div>
    </div>
  );
}
