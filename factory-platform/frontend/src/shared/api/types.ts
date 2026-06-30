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

export type RoleOption = {
  id: string;
  name: string;
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

export type DocumentType =
  | "Manual"
  | "Sop"
  | "PAndId"
  | "MechanicalDrawing"
  | "ElectricalDrawing"
  | "Fat"
  | "Sat"
  | "Certificate"
  | "SparePartCatalog"
  | "Datasheet"
  | "Photo"
  | "Video"
  | "Other";

export type DocumentStatus = "Draft" | "Active" | "Archived" | "Superseded";

export type DocumentVisibility = "Public" | "RoleRestricted" | "AdminOnly";

export type DocumentRecord = {
  id: string;
  title: string;
  description: string;
  documentType: DocumentType;
  revision: string;
  assetId?: string | null;
  assetCode?: string | null;
  assetName?: string | null;
  fileName: string;
  originalFileName: string;
  fileExtension: string;
  fileSize: number;
  uploadedBy: string;
  uploadedByName?: string | null;
  uploadedAt: string;
  lastModified: string;
  status: DocumentStatus;
  visibility: DocumentVisibility;
  allowedRoles: RoleOption[];
};

export type DocumentListResponse = {
  items: DocumentRecord[];
  totalCount: number;
  page: number;
  pageSize: number;
};

export type UpsertDocumentMetadataRequest = {
  title: string;
  description: string;
  documentType: DocumentType;
  revision: string;
  assetId?: string | null;
  status: DocumentStatus;
  visibility: DocumentVisibility;
  allowedRoleIds: string[];
};

export type ProcessTagQuality = "Good" | "Bad" | "Uncertain";

export type WaterTreatmentTank = {
  name: string;
  tagCode: string;
  level: number;
  unit: string;
  status: string;
  lastUpdated: string;
};

export type WaterTreatmentMetric = {
  label: string;
  tagCode: string;
  value: string;
  unit: string;
  quality: ProcessTagQuality;
  lastUpdated: string;
};

export type WaterTreatmentWidgetData = {
  area: string;
  lastUpdated: string;
  tanks: WaterTreatmentTank[];
  feedPump1Status: WaterTreatmentMetric;
  feedPump2Status: WaterTreatmentMetric;
  roPressure: WaterTreatmentMetric;
  roConductivity: WaterTreatmentMetric;
  roFlow: WaterTreatmentMetric;
  softWaterFlow: WaterTreatmentMetric;
  activeAlarms: WaterTreatmentMetric;
};
