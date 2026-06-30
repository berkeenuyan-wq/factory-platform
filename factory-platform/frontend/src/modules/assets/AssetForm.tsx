import type { FormEvent } from "react";
import type { AssetCategory, AssetStatus, UpsertAssetRequest } from "../../shared/api/types";

export const assetCategories: AssetCategory[] = [
  "Machine",
  "Tank",
  "Pump",
  "Valve",
  "Motor",
  "Instrument",
  "Panel",
  "PLC",
  "HMI",
  "Wire"
];

export const assetStatuses: AssetStatus[] = [
  "Planned",
  "Installed",
  "Commissioning",
  "Active",
  "Maintenance",
  "OutOfService"
];

type AssetFormProps = {
  value: UpsertAssetRequest;
  onChange: (value: UpsertAssetRequest) => void;
  onSubmit: () => Promise<void>;
  onCancel: () => void;
  submitLabel: string;
};

export function createEmptyAsset(): UpsertAssetRequest {
  return {
    code: "",
    name: "",
    category: "Machine",
    area: "",
    manufacturer: "",
    model: "",
    serialNumber: "",
    status: "Planned",
    notes: ""
  };
}

export function AssetForm({ value, onChange, onSubmit, onCancel, submitLabel }: AssetFormProps) {
  function update<K extends keyof UpsertAssetRequest>(key: K, nextValue: UpsertAssetRequest[K]) {
    onChange({ ...value, [key]: nextValue });
  }

  function handleSubmit(event: FormEvent) {
    event.preventDefault();
    void onSubmit();
  }

  return (
    <form className="asset-form" onSubmit={handleSubmit}>
      <div className="form-grid">
        <label>
          Code
          <input value={value.code} onChange={(event) => update("code", event.target.value)} required />
        </label>
        <label>
          Name
          <input value={value.name} onChange={(event) => update("name", event.target.value)} required />
        </label>
        <label>
          Category
          <select value={value.category} onChange={(event) => update("category", event.target.value as AssetCategory)}>
            {assetCategories.map((category) => (
              <option key={category} value={category}>
                {category}
              </option>
            ))}
          </select>
        </label>
        <label>
          Status
          <select value={value.status} onChange={(event) => update("status", event.target.value as AssetStatus)}>
            {assetStatuses.map((status) => (
              <option key={status} value={status}>
                {status}
              </option>
            ))}
          </select>
        </label>
        <label>
          Area
          <input value={value.area} onChange={(event) => update("area", event.target.value)} />
        </label>
        <label>
          Manufacturer
          <input value={value.manufacturer} onChange={(event) => update("manufacturer", event.target.value)} />
        </label>
        <label>
          Model
          <input value={value.model} onChange={(event) => update("model", event.target.value)} />
        </label>
        <label>
          Serial Number
          <input value={value.serialNumber} onChange={(event) => update("serialNumber", event.target.value)} />
        </label>
      </div>
      <label>
        Notes
        <textarea value={value.notes} onChange={(event) => update("notes", event.target.value)} />
      </label>
      <div className="form-actions">
        <button className="primary-button" type="submit">
          {submitLabel}
        </button>
        <button type="button" onClick={onCancel}>
          Cancel
        </button>
      </div>
    </form>
  );
}
