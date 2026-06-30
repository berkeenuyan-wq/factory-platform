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
import { AssetsPage } from "../modules/assets/AssetsPage";
import { DashboardPage } from "../modules/dashboard/DashboardPage";
import { DocumentsPage } from "../modules/documents/DocumentsPage";
import { UnderDevelopmentPage } from "../shared/components/UnderDevelopmentPage";

export type FrontendModule = {
  key: string;
  name: string;
  nameKey: string;
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
    nameKey: "module.dashboard",
    route: "/dashboard",
    icon: Home,
    permissions: ["dashboard.view"],
    component: DashboardPage
  },
  {
    key: "assets",
    name: "Assets",
    nameKey: "module.assets",
    route: "/assets",
    icon: Gauge,
    permissions: ["assets.view"],
    component: AssetsPage
  },
  {
    key: "commissioning",
    name: "Commissioning",
    nameKey: "module.commissioning",
    route: "/commissioning",
    icon: ClipboardCheck,
    permissions: ["commissioning.view"],
    component: () => <UnderDevelopmentPage moduleNameKey="module.commissioning" />
  },
  {
    key: "maintenance",
    name: "Maintenance",
    nameKey: "module.maintenance",
    route: "/maintenance",
    icon: Wrench,
    permissions: ["maintenance.view"],
    component: () => <UnderDevelopmentPage moduleNameKey="module.maintenance" />
  },
  {
    key: "warehouse",
    name: "Warehouse",
    nameKey: "module.warehouse",
    route: "/warehouse",
    icon: Package,
    permissions: ["warehouse.view"],
    component: () => <UnderDevelopmentPage moduleNameKey="module.warehouse" />
  },
  {
    key: "documents",
    name: "Documents",
    nameKey: "module.documents",
    route: "/documents",
    icon: FileText,
    permissions: ["documents.view"],
    component: DocumentsPage
  },
  {
    key: "scada",
    name: "SCADA",
    nameKey: "module.scada",
    route: "/scada",
    icon: Activity,
    permissions: ["scada.view"],
    component: () => <UnderDevelopmentPage moduleNameKey="module.scada" />
  },
  {
    key: "reports",
    name: "Reports",
    nameKey: "module.reports",
    route: "/reports",
    icon: BarChart3,
    permissions: ["reports.view"],
    component: () => <UnderDevelopmentPage moduleNameKey="module.reports" />
  },
  {
    key: "settings",
    name: "Settings",
    nameKey: "module.settings",
    route: "/settings",
    icon: Settings,
    permissions: ["settings.view"],
    component: () => <UnderDevelopmentPage moduleNameKey="module.settings" />
  }
];
