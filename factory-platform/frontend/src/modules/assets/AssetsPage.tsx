import { Edit, Eye, Plus, RefreshCw, Search, Trash2 } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { apiRequest } from "../../shared/api/client";
import type { Asset, AssetCategory, AssetStatus, UpsertAssetRequest } from "../../shared/api/types";
import { useAuth } from "../../shared/hooks/useAuth";
import { useLocalization } from "../../shared/i18n/LocalizationProvider";
import { AssetForm, assetCategories, assetStatuses, createEmptyAsset } from "./AssetForm";

type AssetMode = "list" | "create" | "edit" | "detail";

export function AssetsPage() {
  const { token, user } = useAuth();
  const { t } = useLocalization();
  const [assets, setAssets] = useState<Asset[]>([]);
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null);
  const [draft, setDraft] = useState<UpsertAssetRequest>(createEmptyAsset());
  const [mode, setMode] = useState<AssetMode>("list");
  const [search, setSearch] = useState("");
  const [category, setCategory] = useState<AssetCategory | "">("");
  const [status, setStatus] = useState<AssetStatus | "">("");
  const [message, setMessage] = useState(t("assets.ready"));

  const canEditAssets = user?.permissions.includes("assets.edit") ?? false;

  const queryString = useMemo(() => {
    const params = new URLSearchParams();
    if (search.trim()) {
      params.set("search", search.trim());
    }
    if (category) {
      params.set("category", category);
    }
    if (status) {
      params.set("status", status);
    }
    const value = params.toString();
    return value ? `?${value}` : "";
  }, [category, search, status]);

  useEffect(() => {
    void loadAssets();
  }, [queryString, token]);

  async function loadAssets() {
    if (!token) {
      return;
    }

    try {
      const result = await apiRequest<Asset[]>(`/assets${queryString}`, { token });
      setAssets(result);
      setMessage(t("assets.loaded", { count: result.length }));
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  function startCreate() {
    setDraft(createEmptyAsset());
    setSelectedAsset(null);
    setMode("create");
  }

  function startEdit(asset: Asset) {
    setSelectedAsset(asset);
    setDraft(toRequest(asset));
    setMode("edit");
  }

  async function showDetail(asset: Asset) {
    if (!token) {
      return;
    }

    try {
      const detail = await apiRequest<Asset>(`/assets/${asset.id}`, { token });
      setSelectedAsset(detail);
      setMode("detail");
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function createAsset() {
    if (!token || !canEditAssets) {
      return;
    }

    try {
      const created = await apiRequest<Asset>("/assets", {
        method: "POST",
        token,
        body: JSON.stringify(draft)
      });
      setSelectedAsset(created);
      setMode("detail");
      await loadAssets();
      setMessage(`${created.code} created.`);
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function updateAsset() {
    if (!token || !selectedAsset || !canEditAssets) {
      return;
    }

    try {
      const updated = await apiRequest<Asset>(`/assets/${selectedAsset.id}`, {
        method: "PUT",
        token,
        body: JSON.stringify(draft)
      });
      setSelectedAsset(updated);
      setMode("detail");
      await loadAssets();
      setMessage(`${updated.code} updated.`);
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function deleteAsset(asset: Asset) {
    if (!token || !canEditAssets) {
      return;
    }

    try {
      await apiRequest<void>(`/assets/${asset.id}`, {
        method: "DELETE",
        token
      });
      setSelectedAsset(null);
      setMode("list");
      await loadAssets();
      setMessage(`${asset.code} deleted.`);
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  return (
    <section className="assets-page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">{t("assets.eyebrow")}</p>
          <h1>{t("assets.title")}</h1>
        </div>
        <div className="asset-actions">
          <button type="button" onClick={loadAssets}>
            <RefreshCw size={17} />
            {t("common.refresh")}
          </button>
          <button className="primary-button" type="button" onClick={startCreate} disabled={!canEditAssets}>
            <Plus size={18} />
            {t("assets.add")}
          </button>
        </div>
      </div>

      <section className="asset-workspace">
        <aside className="asset-panel">
          <div className="asset-filters">
            <label>
              <span>
                <Search size={15} />
                {t("common.search")}
              </span>
              <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t("assets.searchPlaceholder")} />
            </label>
            <label>
              {t("assets.category")}
              <select value={category} onChange={(event) => setCategory(event.target.value as AssetCategory | "")}>
                <option value="">{t("assets.allCategories")}</option>
                {assetCategories.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
            <label>
              {t("common.status")}
              <select value={status} onChange={(event) => setStatus(event.target.value as AssetStatus | "")}>
                <option value="">{t("assets.allStatuses")}</option>
                {assetStatuses.map((item) => (
                  <option key={item} value={item}>
                    {item}
                  </option>
                ))}
              </select>
            </label>
          </div>

          <div className="asset-table-wrap">
            <table className="asset-table">
              <thead>
                <tr>
                  <th>{t("assets.code")}</th>
                  <th>{t("assets.name")}</th>
                  <th>{t("assets.category")}</th>
                  <th>{t("common.status")}</th>
                  <th>{t("assets.area")}</th>
                  <th>{t("common.actions")}</th>
                </tr>
              </thead>
              <tbody>
                {assets.map((asset) => (
                  <tr key={asset.id}>
                    <td>{asset.code}</td>
                    <td>{asset.name}</td>
                    <td>{asset.category}</td>
                    <td>
                      <span className="status-pill">{asset.status}</span>
                    </td>
                    <td>{asset.area || "-"}</td>
                    <td>
                      <div className="row-actions">
                        <button type="button" aria-label={`View ${asset.code}`} onClick={() => void showDetail(asset)}>
                          <Eye size={15} />
                        </button>
                        <button type="button" aria-label={`Edit ${asset.code}`} onClick={() => startEdit(asset)} disabled={!canEditAssets}>
                          <Edit size={15} />
                        </button>
                        <button type="button" aria-label={`Delete ${asset.code}`} onClick={() => void deleteAsset(asset)} disabled={!canEditAssets}>
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
                {assets.length === 0 && (
                  <tr>
                    <td colSpan={6}>{t("assets.noMatches")}</td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
          <p className="asset-message">{message}</p>
        </aside>

        <section className="asset-detail-panel">
          {mode === "create" && (
            <>
              <h2>{t("assets.addTitle")}</h2>
              <AssetForm value={draft} onChange={setDraft} onSubmit={createAsset} onCancel={() => setMode("list")} submitLabel={t("assets.create")} />
            </>
          )}
          {mode === "edit" && selectedAsset && (
            <>
              <h2>{t("assets.editTitle")}</h2>
              <AssetForm value={draft} onChange={setDraft} onSubmit={updateAsset} onCancel={() => setMode("detail")} submitLabel={t("assets.save")} />
            </>
          )}
          {mode === "detail" && selectedAsset && (
            <AssetDetail asset={selectedAsset} onEdit={() => startEdit(selectedAsset)} canEdit={canEditAssets} />
          )}
          {mode === "list" && (
            <div className="empty-state compact">{t("assets.selectOrCreate")}</div>
          )}
        </section>
      </section>
    </section>
  );
}

function AssetDetail({ asset, canEdit, onEdit }: { asset: Asset; canEdit: boolean; onEdit: () => void }) {
  const { t } = useLocalization();
  return (
    <div className="asset-detail">
      <div className="asset-detail-header">
        <div>
          <p className="eyebrow">{asset.category}</p>
          <h2>{asset.name}</h2>
        </div>
        <button type="button" onClick={onEdit} disabled={!canEdit}>
          <Edit size={16} />
          {t("common.edit")}
        </button>
      </div>
      <dl>
        <div>
          <dt>{t("assets.code")}</dt>
          <dd>{asset.code}</dd>
        </div>
        <div>
          <dt>{t("common.status")}</dt>
          <dd>{asset.status}</dd>
        </div>
        <div>
          <dt>{t("assets.area")}</dt>
          <dd>{asset.area || "-"}</dd>
        </div>
        <div>
          <dt>{t("assets.manufacturer")}</dt>
          <dd>{asset.manufacturer || "-"}</dd>
        </div>
        <div>
          <dt>{t("assets.model")}</dt>
          <dd>{asset.model || "-"}</dd>
        </div>
        <div>
          <dt>{t("assets.serialNumber")}</dt>
          <dd>{asset.serialNumber || "-"}</dd>
        </div>
        <div className="wide">
          <dt>{t("assets.notes")}</dt>
          <dd>{asset.notes || "-"}</dd>
        </div>
      </dl>
    </div>
  );
}

function toRequest(asset: Asset): UpsertAssetRequest {
  return {
    code: asset.code,
    name: asset.name,
    category: asset.category,
    area: asset.area,
    manufacturer: asset.manufacturer,
    model: asset.model,
    serialNumber: asset.serialNumber,
    status: asset.status,
    notes: asset.notes
  };
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Asset request failed.";
}
