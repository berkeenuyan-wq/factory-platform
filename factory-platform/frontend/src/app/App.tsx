import { useMemo, useState } from "react";
import { AuthProvider, useAuth } from "../shared/hooks/useAuth";
import { AppShell } from "../shared/layouts/AppShell";
import { LoginPage } from "./LoginPage";
import { moduleRegistry } from "./moduleRegistry";
import { canAccess } from "../shared/permissions/permissions";

function AuthenticatedApp() {
  const { user, isLoading } = useAuth();
  const availableModules = useMemo(
    () => moduleRegistry.filter((module) => canAccess(module.permissions, user?.permissions ?? [])),
    [user?.permissions]
  );
  const [activeRoute, setActiveRoute] = useState("/dashboard");

  if (isLoading) {
    return <div className="loading-screen">Starting platform...</div>;
  }

  if (!user) {
    return <LoginPage />;
  }

  const activeModule = availableModules.find((module) => module.route === activeRoute) ?? availableModules[0];
  const ActivePage = activeModule?.component ?? (() => <div>No accessible modules.</div>);

  return (
    <AppShell modules={availableModules} activeRoute={activeModule?.route ?? ""} onNavigate={setActiveRoute}>
      <ActivePage />
    </AppShell>
  );
}

export function App() {
  return (
    <AuthProvider>
      <AuthenticatedApp />
    </AuthProvider>
  );
}
