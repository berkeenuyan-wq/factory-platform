import { LogOut, Menu } from "lucide-react";
import type { ReactNode } from "react";
import type { FrontendModule } from "../../app/moduleRegistry";
import { useAuth } from "../hooks/useAuth";
import { useLocalization } from "../i18n/LocalizationProvider";
import { languages, LanguageCode } from "../i18n/translations";

type AppShellProps = {
  modules: FrontendModule[];
  activeRoute: string;
  onNavigate: (route: string) => void;
  children: ReactNode;
};

export function AppShell({ modules, activeRoute, onNavigate, children }: AppShellProps) {
  const { user, logout } = useAuth();
  const { language, setLanguage, t } = useLocalization();

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-header">
          <div className="brand-emblem">FO</div>
          <div>
            <strong>FactoryOS</strong>
            <span>{t("app.version")}</span>
          </div>
        </div>
        <nav className="sidebar-nav" aria-label={t("app.menu")}>
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
                <span>{t(module.nameKey)}</span>
              </button>
            );
          })}
        </nav>
      </aside>
      <div className="main-frame">
        <header className="topbar">
          <button className="icon-button" type="button" aria-label={t("app.menu")}>
            <Menu size={20} />
          </button>
          <div className="environment-pill">{t("app.network")}</div>
          <label className="language-picker">
            <span>{t("language.label")}</span>
            <select value={language} onChange={(event) => setLanguage(event.target.value as LanguageCode)}>
              {languages.map((item) => (
                <option key={item.code} value={item.code}>{item.label}</option>
              ))}
            </select>
          </label>
          <div className="profile-area">
            <div>
              <strong>{user?.displayName}</strong>
              <span>{user?.roles.join(", ")}</span>
            </div>
            <button className="logout-button" onClick={logout} type="button">
              <LogOut size={18} />
              {t("app.logout")}
            </button>
          </div>
        </header>
        <main className="content-area">{children}</main>
      </div>
    </div>
  );
}
