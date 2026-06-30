export type User = {
  id: string;
  email: string;
  displayName: string;
  roles: string[];
  permissions: string[];
};

export type ModuleDefinition = {
  key: string;
  name: string;
  route: string;
  icon: string;
  permissions: string[];
  order: number;
};

export type DashboardLayout = {
  id: string;
  userId: string;
  name: string;
  layoutJson: string;
  updatedAtUtc: string;
};

export type UserSettings = {
  id: string;
  userId: string;
  settingsJson: string;
};

export type AuditLog = {
  id: string;
  action: string;
  entityName: string;
  entityId?: string;
  userId?: string;
  createdAtUtc: string;
};

export type AssetCategory =
  | "Machine"
  | "Tank"
  | "Pump"
  | "Valve"
  | "Motor"
  | "Instrument"
  | "Panel"
  | "PLC"
  | "HMI"
  | "Wire";

export type AssetStatus =
  | "Planned"
  | "Installed"
  | "Commissioning"
  | "Active"
  | "Maintenance"
  | "OutOfService";

export type Asset = {
  id: string;
  code: string;
  name: string;
  category: AssetCategory;
  area: string;
  manufacturer: string;
  model: string;
  serialNumber: string;
  status: AssetStatus;
  notes: string;
  createdAtUtc: string;
  updatedAtUtc: string;
};

export type UpsertAssetRequest = Omit<Asset, "id" | "createdAtUtc" | "updatedAtUtc">;
