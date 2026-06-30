import { Download, Edit, Eye, FileUp, RefreshCw, Search, Trash2, X } from "lucide-react";
import { FormEvent, useEffect, useMemo, useState } from "react";
import { API_BASE_URL, apiRequest } from "../../shared/api/client";
import type { Asset, DocumentListResponse, DocumentRecord, DocumentStatus, DocumentType, DocumentVisibility, RoleOption, UpsertDocumentMetadataRequest } from "../../shared/api/types";
import { useAuth } from "../../shared/hooks/useAuth";
import { useLocalization } from "../../shared/i18n/LocalizationProvider";

const documentTypes: DocumentType[] = ["Manual", "Sop", "PAndId", "MechanicalDrawing", "ElectricalDrawing", "Fat", "Sat", "Certificate", "SparePartCatalog", "Datasheet", "Photo", "Video", "Other"];
const documentStatuses: DocumentStatus[] = ["Draft", "Active", "Archived", "Superseded"];
const visibilityOptions: DocumentVisibility[] = ["Public", "RoleRestricted", "AdminOnly"];

type PanelMode = "none" | "upload" | "edit" | "detail" | "delete";
type UploadDraft = UpsertDocumentMetadataRequest & { file: File | null };

const emptyDraft: UploadDraft = {
  title: "",
  description: "",
  documentType: "Manual",
  revision: "A",
  assetId: "",
  status: "Draft",
  visibility: "Public",
  allowedRoleIds: [],
  file: null
};

export function DocumentsPage() {
  const { token, user } = useAuth();
  const { t } = useLocalization();
  const [documents, setDocuments] = useState<DocumentRecord[]>([]);
  const [roles, setRoles] = useState<RoleOption[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [selectedDocument, setSelectedDocument] = useState<DocumentRecord | null>(null);
  const [draft, setDraft] = useState<UploadDraft>(emptyDraft);
  const [mode, setMode] = useState<PanelMode>("none");
  const [search, setSearch] = useState("");
  const [documentType, setDocumentType] = useState<DocumentType | "">("");
  const [status, setStatus] = useState<DocumentStatus | "">("");
  const [assetId, setAssetId] = useState("");
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const [sortBy, setSortBy] = useState("uploadedAt");
  const [sortDirection, setSortDirection] = useState<"asc" | "desc">("desc");
  const [totalCount, setTotalCount] = useState(0);
  const [message, setMessage] = useState(t("documents.ready"));

  const canUpload = user?.permissions.includes("documents.upload") ?? false;
  const canEdit = user?.permissions.includes("documents.edit") ?? false;
  const canDelete = user?.permissions.includes("documents.delete") ?? false;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));

  const queryString = useMemo(() => {
    const params = new URLSearchParams();
    if (search.trim()) params.set("search", search.trim());
    if (documentType) params.set("documentType", documentType);
    if (status) params.set("status", status);
    if (assetId) params.set("assetId", assetId);
    params.set("page", String(page));
    params.set("pageSize", String(pageSize));
    params.set("sortBy", sortBy);
    params.set("sortDirection", sortDirection);
    return params.toString();
  }, [assetId, documentType, page, pageSize, search, sortBy, sortDirection, status]);

  useEffect(() => {
    void loadReferenceData();
  }, [token]);

  useEffect(() => {
    void loadDocuments();
  }, [queryString, token]);

  async function loadReferenceData() {
    if (!token) return;
    try {
      const [roleOptions, assetOptions] = await Promise.all([
        apiRequest<RoleOption[]>("/roles/options", { token }),
        apiRequest<Asset[]>("/assets", { token })
      ]);
      setRoles(roleOptions);
      setAssets(assetOptions);
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function loadDocuments() {
    if (!token) return;
    try {
      const result = await apiRequest<DocumentListResponse>(`/documents?${queryString}`, { token });
      setDocuments(result.items);
      setTotalCount(result.totalCount);
      setMessage(t("documents.available", { count: result.totalCount }));
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  function openUpload() {
    setDraft({ ...emptyDraft });
    setSelectedDocument(null);
    setMode("upload");
  }

  async function openDetail(document: DocumentRecord) {
    if (!token) return;
    try {
      const detail = await apiRequest<DocumentRecord>(`/documents/${document.id}`, { token });
      setSelectedDocument(detail);
      setDraft(toDraft(detail));
      setMode("detail");
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  function openEdit(document: DocumentRecord) {
    setSelectedDocument(document);
    setDraft(toDraft(document));
    setMode("edit");
  }

  async function uploadDocument(event: FormEvent) {
    event.preventDefault();
    if (!token || !draft.file) return;

    const form = new FormData();
    form.set("file", draft.file);
    form.set("title", draft.title);
    form.set("description", draft.description);
    form.set("documentType", draft.documentType);
    form.set("revision", draft.revision);
    form.set("assetId", draft.assetId ?? "");
    form.set("status", draft.status);
    form.set("visibility", draft.visibility);
    form.set("allowedRoleIds", JSON.stringify(draft.allowedRoleIds));

    try {
      const created = await apiRequest<DocumentRecord>("/documents/upload", { method: "POST", token, body: form });
      setSelectedDocument(created);
      setMode("detail");
      await loadDocuments();
      setMessage(t("documents.uploadedMessage", { title: created.title }));
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function updateDocument(event: FormEvent) {
    event.preventDefault();
    if (!token || !selectedDocument) return;
    try {
      const updated = await apiRequest<DocumentRecord>(`/documents/${selectedDocument.id}`, {
        method: "PUT",
        token,
        body: JSON.stringify(toMetadataRequest(draft))
      });
      setSelectedDocument(updated);
      setMode("detail");
      await loadDocuments();
      setMessage(t("documents.updatedMessage", { title: updated.title }));
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function deleteDocument() {
    if (!token || !selectedDocument) return;
    try {
      await apiRequest<void>(`/documents/${selectedDocument.id}`, { method: "DELETE", token });
      setSelectedDocument(null);
      setMode("none");
      await loadDocuments();
      setMessage(t("documents.deletedMessage"));
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  async function downloadDocument(document: DocumentRecord) {
    if (!token) return;
    try {
      const response = await fetch(`${API_BASE_URL}/documents/${document.id}/download`, { headers: { Authorization: `Bearer ${token}` } });
      if (!response.ok) throw new Error((await response.text()) || `Download failed with ${response.status}`);
      const blob = await response.blob();
      const url = URL.createObjectURL(blob);
      const link = window.document.createElement("a");
      link.href = url;
      link.download = document.originalFileName;
      link.click();
      URL.revokeObjectURL(url);
      setMessage(t("documents.downloadedMessage", { name: document.originalFileName }));
    } catch (error) {
      setMessage(getErrorMessage(error));
    }
  }

  function resetFilters() {
    setSearch("");
    setDocumentType("");
    setStatus("");
    setAssetId("");
    setPage(1);
  }

  return (
    <section className="documents-page">
      <div className="page-heading">
        <div>
          <p className="eyebrow">{t("documents.eyebrow")}</p>
          <h1>{t("documents.title")}</h1>
        </div>
        <div className="asset-actions">
          <button type="button" onClick={() => void loadDocuments()}><RefreshCw size={17} />{t("common.refresh")}</button>
          <button className="primary-button" type="button" onClick={openUpload} disabled={!canUpload}><FileUp size={18} />{t("documents.upload")}</button>
        </div>
      </div>

      <section className="document-workspace">
        <aside className="document-panel">
          <div className="document-filters">
            <label className="wide-filter"><span><Search size={15} />{t("common.search")}</span><input value={search} onChange={(event) => { setSearch(event.target.value); setPage(1); }} placeholder={t("documents.searchPlaceholder")} /></label>
            <label>{t("common.type")}<select value={documentType} onChange={(event) => { setDocumentType(event.target.value as DocumentType | ""); setPage(1); }}><option value="">{t("documents.allTypes")}</option>{documentTypes.map((type) => <option key={type} value={type}>{formatDocumentType(type)}</option>)}</select></label>
            <label>{t("common.status")}<select value={status} onChange={(event) => { setStatus(event.target.value as DocumentStatus | ""); setPage(1); }}><option value="">{t("documents.allStatuses")}</option>{documentStatuses.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
            <label>{t("documents.linkedAsset")}<select value={assetId} onChange={(event) => { setAssetId(event.target.value); setPage(1); }}><option value="">{t("documents.allAssets")}</option>{assets.map((asset) => <option key={asset.id} value={asset.id}>{asset.code} - {asset.name}</option>)}</select></label>
            <label>{t("documents.sort")}<select value={sortBy} onChange={(event) => setSortBy(event.target.value)}><option value="uploadedAt">{t("documents.uploaded")}</option><option value="title">{t("documents.titleColumn")}</option><option value="type">{t("common.type")}</option><option value="status">{t("common.status")}</option><option value="revision">{t("documents.revision")}</option></select></label>
            <label>{t("documents.direction")}<select value={sortDirection} onChange={(event) => setSortDirection(event.target.value as "asc" | "desc")}><option value="desc">{t("documents.desc")}</option><option value="asc">{t("documents.asc")}</option></select></label>
            <button type="button" onClick={resetFilters}><X size={16} />{t("common.clear")}</button>
          </div>

          <div className="document-table-wrap">
            <table className="asset-table document-table">
              <thead><tr><th>{t("documents.titleColumn")}</th><th>{t("common.type")}</th><th>{t("documents.revision")}</th><th>{t("common.status")}</th><th>{t("documents.linkedAsset")}</th><th>{t("documents.visibility")}</th><th>{t("common.actions")}</th></tr></thead>
              <tbody>
                {documents.map((document) => (
                  <tr key={document.id}>
                    <td><strong>{document.title}</strong><span>{document.originalFileName}</span></td>
                    <td>{formatDocumentType(document.documentType)}</td>
                    <td>{document.revision}</td>
                    <td><span className="status-pill">{document.status}</span></td>
                    <td>{document.assetCode ?? "-"}</td>
                    <td>{formatVisibility(document)}</td>
                    <td><div className="row-actions">
                      <button type="button" aria-label={`View ${document.title}`} onClick={() => void openDetail(document)}><Eye size={15} /></button>
                      <button type="button" aria-label={`Download ${document.title}`} onClick={() => void downloadDocument(document)}><Download size={15} /></button>
                      <button type="button" aria-label={`Edit ${document.title}`} onClick={() => { setSelectedDocument(document); setDraft(toDraft(document)); setMode("edit"); }} disabled={!canEdit}><Edit size={15} /></button>
                      <button type="button" aria-label={`Delete ${document.title}`} onClick={() => { setSelectedDocument(document); setMode("delete"); }} disabled={!canDelete}><Trash2 size={15} /></button>
                    </div></td>
                  </tr>
                ))}
                {documents.length === 0 && <tr><td colSpan={7}>{t("documents.noMatches")}</td></tr>}
              </tbody>
            </table>
          </div>

          <div className="document-pagination">
            <span>{t("documents.pageOf", { page, total: totalPages })}</span>
            <button type="button" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page <= 1}>{t("common.previous")}</button>
            <button type="button" onClick={() => setPage((value) => Math.min(totalPages, value + 1))} disabled={page >= totalPages}>{t("common.next")}</button>
            <select value={pageSize} onChange={(event) => { setPageSize(Number(event.target.value)); setPage(1); }}><option value={10}>{t("documents.pageSize", { size: 10 })}</option><option value={20}>{t("documents.pageSize", { size: 20 })}</option><option value={50}>{t("documents.pageSize", { size: 50 })}</option></select>
          </div>
          <p className="asset-message">{message}</p>
        </aside>

        <section className="document-detail-panel">
          {mode === "upload" && <DocumentForm title={t("documents.uploadTitle")} draft={draft} roles={roles} assets={assets} submitLabel={t("documents.upload")} requiresFile onChange={setDraft} onSubmit={uploadDocument} onCancel={() => setMode("none")} />}
          {mode === "edit" && selectedDocument && <DocumentForm title={t("documents.editTitle")} draft={draft} roles={roles} assets={assets} submitLabel={t("common.save")} onChange={setDraft} onSubmit={updateDocument} onCancel={() => setMode("detail")} />}
          {mode === "detail" && selectedDocument && <DocumentDetail document={selectedDocument} onDownload={() => void downloadDocument(selectedDocument)} onEdit={() => { setDraft(toDraft(selectedDocument)); setMode("edit"); }} canEdit={canEdit} />}
          {mode === "delete" && selectedDocument && <div className="document-confirm"><h2>{t("documents.deleteTitle")}</h2><p>{t("documents.deleteConfirm", { title: selectedDocument.title })}</p><div className="form-actions"><button className="danger-button" type="button" onClick={() => void deleteDocument()}>{t("common.delete")}</button><button type="button" onClick={() => setMode("detail")}>{t("common.cancel")}</button></div></div>}
          {mode === "none" && <div className="empty-state compact">{t("documents.selectOrUpload")}</div>}
        </section>
      </section>
    </section>
  );
}

function DocumentForm({ title, draft, roles, assets, submitLabel, requiresFile, onChange, onSubmit, onCancel }: { title: string; draft: UploadDraft; roles: RoleOption[]; assets: Asset[]; submitLabel: string; requiresFile?: boolean; onChange: (next: UploadDraft) => void; onSubmit: (event: FormEvent) => void; onCancel: () => void }) {
  const { t } = useLocalization();

  function update<K extends keyof UploadDraft>(key: K, value: UploadDraft[K]) {
    const next = { ...draft, [key]: value };
    if (key === "visibility" && value !== "RoleRestricted") next.allowedRoleIds = [];
    onChange(next);
  }

  function toggleRole(roleId: string) {
    update("allowedRoleIds", draft.allowedRoleIds.includes(roleId) ? draft.allowedRoleIds.filter((id) => id !== roleId) : [...draft.allowedRoleIds, roleId]);
  }

  return (
    <form className="asset-form document-form" onSubmit={onSubmit}>
      <h2>{title}</h2>
      <div className="form-grid">
        <label>{t("documents.titleColumn")}<input value={draft.title} onChange={(event) => update("title", event.target.value)} required /></label>
        <label>{t("documents.revision")}<input value={draft.revision} onChange={(event) => update("revision", event.target.value)} required /></label>
        <label>{t("common.type")}<select value={draft.documentType} onChange={(event) => update("documentType", event.target.value as DocumentType)}>{documentTypes.map((type) => <option key={type} value={type}>{formatDocumentType(type)}</option>)}</select></label>
        <label>{t("common.status")}<select value={draft.status} onChange={(event) => update("status", event.target.value as DocumentStatus)}>{documentStatuses.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
        <label>{t("documents.linkedAsset")}<select value={draft.assetId ?? ""} onChange={(event) => update("assetId", event.target.value || null)}><option value="">{t("common.noAsset")}</option>{assets.map((asset) => <option key={asset.id} value={asset.id}>{asset.code} - {asset.name}</option>)}</select></label>
        <label>{t("documents.visibility")}<select value={draft.visibility} onChange={(event) => update("visibility", event.target.value as DocumentVisibility)}>{visibilityOptions.map((item) => <option key={item} value={item}>{item}</option>)}</select></label>
        {requiresFile && <label className="wide">{t("documents.file")}<input type="file" onChange={(event) => update("file", event.target.files?.[0] ?? null)} required /></label>}
      </div>
      <label>{t("documents.description")}<textarea value={draft.description} onChange={(event) => update("description", event.target.value)} /></label>
      {draft.visibility === "RoleRestricted" && <fieldset className="role-selector"><legend>{t("documents.allowedRoles")}</legend>{roles.map((role) => <label key={role.id}><input type="checkbox" checked={draft.allowedRoleIds.includes(role.id)} onChange={() => toggleRole(role.id)} />{role.name}</label>)}</fieldset>}
      <div className="form-actions"><button className="primary-button" type="submit">{submitLabel}</button><button type="button" onClick={onCancel}>{t("common.cancel")}</button></div>
    </form>
  );
}

function DocumentDetail({ document, canEdit, onDownload, onEdit }: { document: DocumentRecord; canEdit: boolean; onDownload: () => void; onEdit: () => void }) {
  const { t } = useLocalization();

  return (
    <div className="asset-detail document-detail">
      <div className="asset-detail-header">
        <div><p className="eyebrow">{formatDocumentType(document.documentType)} · {t("documents.revision")} {document.revision}</p><h2>{document.title}</h2></div>
        <div className="asset-actions"><button type="button" onClick={onDownload}><Download size={16} />{t("documents.download")}</button><button type="button" onClick={onEdit} disabled={!canEdit}><Edit size={16} />{t("common.edit")}</button></div>
      </div>
      <dl>
        <div><dt>{t("common.status")}</dt><dd>{document.status}</dd></div><div><dt>{t("documents.visibility")}</dt><dd>{formatVisibility(document)}</dd></div><div><dt>{t("documents.file")}</dt><dd>{document.originalFileName}</dd></div><div><dt>{t("documents.size")}</dt><dd>{formatBytes(document.fileSize)}</dd></div>
        <div><dt>{t("documents.linkedAsset")}</dt><dd>{document.assetCode ? `${document.assetCode} - ${document.assetName}` : "-"}</dd></div><div><dt>{t("documents.uploadedBy")}</dt><dd>{document.uploadedByName ?? "-"}</dd></div><div><dt>{t("documents.uploaded")}</dt><dd>{formatDate(document.uploadedAt)}</dd></div><div><dt>{t("documents.modified")}</dt><dd>{formatDate(document.lastModified)}</dd></div>
        <div className="wide"><dt>{t("documents.description")}</dt><dd>{document.description || "-"}</dd></div>
      </dl>
    </div>
  );
}
function toDraft(document: DocumentRecord): UploadDraft {
  return { title: document.title, description: document.description, documentType: document.documentType, revision: document.revision, assetId: document.assetId ?? "", status: document.status, visibility: document.visibility, allowedRoleIds: document.allowedRoles.map((role) => role.id), file: null };
}

function toMetadataRequest(draft: UploadDraft): UpsertDocumentMetadataRequest {
  return { title: draft.title, description: draft.description, documentType: draft.documentType, revision: draft.revision, assetId: draft.assetId || null, status: draft.status, visibility: draft.visibility, allowedRoleIds: draft.allowedRoleIds };
}

function formatDocumentType(type: DocumentType) {
  const labels: Record<DocumentType, string> = { Manual: "Manual", Sop: "SOP", PAndId: "P&ID", MechanicalDrawing: "Mechanical Drawing", ElectricalDrawing: "Electrical Drawing", Fat: "FAT", Sat: "SAT", Certificate: "Certificate", SparePartCatalog: "Spare Part Catalog", Datasheet: "Datasheet", Photo: "Photo", Video: "Video", Other: "Other" };
  return labels[type];
}

function formatVisibility(document: DocumentRecord) {
  return document.visibility === "RoleRestricted" ? `RoleRestricted (${document.allowedRoles.map((role) => role.name).join(", ") || "none"})` : document.visibility;
}

function formatBytes(value: number) {
  if (value < 1024) return `${value} B`;
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`;
  return `${(value / 1024 / 1024).toFixed(1)} MB`;
}

function formatDate(value: string) {
  return new Date(value).toLocaleString();
}

function getErrorMessage(error: unknown) {
  return error instanceof Error ? error.message : "Document request failed.";
}


