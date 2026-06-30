import {
  Activity,
  BarChart3,
  ClipboardCheck,
  FileText,
  Gauge,
  Home,
  Package,
  Settings,
  Wrench
} from "lucide-react";
import type { ComponentType } from "react";
import { DashboardPage } from "../modules/dashboard/DashboardPage";
import { UnderDevelopmentPage } from "../shared/components/UnderDevelopmentPage";

export type FrontendModule = {
  key: string;
  name: string;
  route: string;
  icon: ComponentType<{ size?: number }>;
  permissions: string[];
  component: ComponentType;
};

// Future modules should be registered here or composed from a remote module manifest.
// The sidebar consumes this registry and must not hard-code navigation items.
export const moduleRegistry: FrontendModule[] = [
  {
    key: "dashboard",
    name: "Dashboard",
    route: "/dashboard",
    icon: Home,
    permissions: ["dashboard.view"],
    component: DashboardPage
  },
  {
    key: "assets",
    name: "Assets",
    route: "/assets",
    icon: Gauge,
    permissions: ["assets.view"],
    component: () => <UnderDevelopmentPage moduleName="Assets" />
  },
  {
    key: "commissioning",
    name: "Commissioning",
    route: "/commissioning",
    icon: ClipboardCheck,
    permissions: ["commissioning.view"],
    component: () => <UnderDevelopmentPage moduleName="Commissioning" />
  },
  {
    key: "maintenance",
    name: "Maintenance",
    route: "/maintenance",
    icon: Wrench,
    permissions: ["maintenance.view"],
    component: () => <UnderDevelopmentPage moduleName="Maintenance" />
  },
  {
    key: "warehouse",
    name: "Warehouse",
    route: "/warehouse",
    icon: Package,
    permissions: ["warehouse.view"],
    component: () => <UnderDevelopmentPage moduleName="Warehouse" />
  },
  {
    key: "documents",
    name: "Documents",
    route: "/documents",
    icon: FileText,
    permissions: ["documents.view"],
    component: () => <UnderDevelopmentPage moduleName="Documents" />
  },
  {
    key: "scada",
    name: "SCADA",
    route: "/scada",
    icon: Activity,
    permissions: ["scada.view"],
    component: () => <UnderDevelopmentPage moduleName="SCADA" />
  },
  {
    key: "reports",
    name: "Reports",
    route: "/reports",
    icon: BarChart3,
    permissions: ["reports.view"],
    component: () => <UnderDevelopmentPage moduleName="Reports" />
  },
  {
    key: "settings",
    name: "Settings",
    route: "/settings",
    icon: Settings,
    permissions: ["settings.view"],
    component: () => <UnderDevelopmentPage moduleName="Settings" />
  }
];
