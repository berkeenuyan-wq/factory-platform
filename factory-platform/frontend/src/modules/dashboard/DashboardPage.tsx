import { useEffect, useMemo, useState } from "react";
import { ArrowLeft, ArrowRight, GripVertical, Library, Plus, Save, Trash2 } from "lucide-react";
import { apiRequest } from "../../shared/api/client";
import type { DashboardLayout } from "../../shared/api/types";
import { useAuth } from "../../shared/hooks/useAuth";
import { useLocalization } from "../../shared/i18n/LocalizationProvider";
import { widgetRegistry } from "./widgetRegistry";

type DashboardWidgetLayout = {
  id: string;
  type: string;
  width: 1 | 2 | 4;
  height: 1 | 2;
  config?: unknown;
};

type LegacyDashboardWidgetLayout = {
  id: string;
  type: string;
  size?: "small" | "medium" | "wide";
  width?: 1 | 2 | 4;
  height?: 1 | 2;
  config?: unknown;
};

const selectedDashboardStorageKey = "factory-platform-selected-dashboard";

const defaultLayout: DashboardWidgetLayout[] = widgetRegistry.map((widget, index) => ({
  id: `${widget.type}-${index}`,
  type: widget.type,
  width: widget.defaultWidth,
  height: widget.defaultHeight,
  config: widget.defaultConfig
}));

function normalizeWidgetLayout(layout: LegacyDashboardWidgetLayout[]): DashboardWidgetLayout[] {
  return layout.map((widget) => ({
    id: widget.id,
    type: widget.type,
    width: widget.width ?? (widget.size === "wide" ? 4 : widget.size === "medium" ? 2 : 1),
    height: widget.height ?? 1,
    config: widget.config
  }));
}

function parseLayout(layoutJson: string) {
  return normalizeWidgetLayout(JSON.parse(layoutJson) as LegacyDashboardWidgetLayout[]);
}

export function DashboardPage() {
  const { token, user } = useAuth();
  const { t } = useLocalization();
  const [dashboards, setDashboards] = useState<DashboardLayout[]>([]);
  const [activeDashboardId, setActiveDashboardId] = useState<string | null>(null);
  const [widgets, setWidgets] = useState<DashboardWidgetLayout[]>(defaultLayout);
  const [rawJson, setRawJson] = useState(JSON.stringify(defaultLayout, null, 2));
  const [newDashboardName, setNewDashboardName] = useState("Operations");
  const [draggedWidgetId, setDraggedWidgetId] = useState<string | null>(null);
  const [status, setStatus] = useState(t("dashboard.loading"));

  const canEditDashboard = user?.permissions.includes("dashboard.edit") ?? false;
  const registryByType = useMemo(() => new Map(widgetRegistry.map((widget) => [widget.type, widget])), []);
  const activeDashboard = dashboards.find((dashboard) => dashboard.id === activeDashboardId);

  useEffect(() => {
    if (!token) {
      return;
    }

    apiRequest<DashboardLayout[]>("/dashboard/layouts", { token })
      .then((layouts) => {
        setDashboards(layouts);
        const storedId = localStorage.getItem(selectedDashboardStorageKey);
        const selected = layouts.find((layout) => layout.id === storedId) ?? layouts[0];
        if (selected) {
          loadDashboard(selected);
        }
      })
      .catch((error) => setStatus(error instanceof Error ? error.message : "Could not load dashboards."));
  }, [token]);

  function loadDashboard(dashboard: DashboardLayout) {
    try {
      const parsed = parseLayout(dashboard.layoutJson);
      setActiveDashboardId(dashboard.id);
      setWidgets(parsed);
      setRawJson(JSON.stringify(parsed, null, 2));
      localStorage.setItem(selectedDashboardStorageKey, dashboard.id);
      setStatus(t("dashboard.loaded", { name: dashboard.name }));
    } catch {
      setStatus(t("dashboard.invalidJson", { name: dashboard.name }));
    }
  }

  function updateWidgets(nextWidgets: DashboardWidgetLayout[], nextStatus: string) {
    setWidgets(nextWidgets);
    setRawJson(JSON.stringify(nextWidgets, null, 2));
    setStatus(nextStatus);
  }

  function addWidget(type: string) {
    const definition = registryByType.get(type);
    if (!definition || !canEditDashboard) {
      return;
    }

    updateWidgets(
      [
        ...widgets,
        {
          id: `${definition.type}-${crypto.randomUUID()}`,
          type: definition.type,
          width: definition.defaultWidth,
          height: definition.defaultHeight,
          config: definition.defaultConfig
        }
      ],
      t("dashboard.widgetAdded", { name: t(definition.titleKey) })
    );
  }

  function removeWidget(widgetId: string) {
    updateWidgets(
      widgets.filter((widget) => widget.id !== widgetId),
      t("dashboard.widgetRemoved")
    );
  }

  function resizeWidget(widgetId: string, dimension: "width" | "height", direction: 1 | -1) {
    const widthSteps: Array<DashboardWidgetLayout["width"]> = [1, 2, 4];
    const heightSteps: Array<DashboardWidgetLayout["height"]> = [1, 2];
    const nextWidgets = widgets.map((widget) => {
      if (widget.id !== widgetId) {
        return widget;
      }

      if (dimension === "width") {
        const currentIndex = widthSteps.indexOf(widget.width);
        return { ...widget, width: widthSteps[Math.max(0, Math.min(widthSteps.length - 1, currentIndex + direction))] };
      }

      const currentIndex = heightSteps.indexOf(widget.height);
      return { ...widget, height: heightSteps[Math.max(0, Math.min(heightSteps.length - 1, currentIndex + direction))] };
    });

    updateWidgets(nextWidgets, t("dashboard.widgetResized"));
  }

  function moveWidget(targetWidgetId: string) {
    if (!draggedWidgetId || draggedWidgetId === targetWidgetId || !canEditDashboard) {
      return;
    }

    const draggedIndex = widgets.findIndex((widget) => widget.id === draggedWidgetId);
    const targetIndex = widgets.findIndex((widget) => widget.id === targetWidgetId);
    if (draggedIndex < 0 || targetIndex < 0) {
      return;
    }

    const nextWidgets = [...widgets];
    const [draggedWidget] = nextWidgets.splice(draggedIndex, 1);
    nextWidgets.splice(targetIndex, 0, draggedWidget);
    updateWidgets(nextWidgets, t("dashboard.widgetMoved"));
    setDraggedWidgetId(null);
  }

  function moveWidgetByDirection(widgetId: string, direction: -1 | 1) {
    if (!canEditDashboard) {
      return;
    }

    const currentIndex = widgets.findIndex((widget) => widget.id === widgetId);
    const nextIndex = currentIndex + direction;
    if (currentIndex < 0 || nextIndex < 0 || nextIndex >= widgets.length) {
      return;
    }

    const nextWidgets = [...widgets];
    const [widget] = nextWidgets.splice(currentIndex, 1);
    nextWidgets.splice(nextIndex, 0, widget);
    updateWidgets(nextWidgets, t("dashboard.widgetMoved"));
  }

  function applyJson() {
    try {
      const parsed = parseLayout(rawJson);
      updateWidgets(parsed, t("dashboard.previewUpdated"));
    } catch {
      setStatus(t("dashboard.layoutInvalid"));
    }
  }

  async function saveLayout() {
    if (!token || !activeDashboard || !canEditDashboard) {
      return;
    }

    const layoutJson = JSON.stringify(widgets, null, 2);
    const saved = await apiRequest<DashboardLayout>(`/dashboard/layouts/${activeDashboard.id}`, {
      method: "PUT",
      token,
      body: JSON.stringify({ name: activeDashboard.name, layoutJson })
    });

    setDashboards((items) => items.map((item) => (item.id === saved.id ? saved : item)));
    setRawJson(layoutJson);
    setStatus(t("dashboard.layoutSaved"));
  }

  async function createDashboard() {
    if (!token || !canEditDashboard) {
      return;
    }

    const created = await apiRequest<DashboardLayout>("/dashboard/layouts", {
      method: "POST",
      token,
      body: JSON.stringify({
        name: newDashboardName,
        layoutJson: JSON.stringify(defaultLayout, null, 2)
      })
    });

    setDashboards((items) => [...items, created]);
    loadDashboard(created);
    setNewDashboardName("");
    setStatus(t("dashboard.created", { name: created.name }));
  }

  return (
    <section className="dashboard-page">
      <div className="page-heading dashboard-heading">
        <div>
          <p className="eyebrow">{t("dashboard.eyebrow")}</p>
          <h1>{t("dashboard.title")}</h1>
        </div>
        <div className="dashboard-actions">
          <select
            value={activeDashboardId ?? ""}
            onChange={(event) => {
              const selected = dashboards.find((dashboard) => dashboard.id === event.target.value);
              if (selected) {
                loadDashboard(selected);
              }
            }}
            aria-label={t("dashboard.dashboard")}
          >
            {dashboards.map((dashboard) => (
              <option key={dashboard.id} value={dashboard.id}>
                {dashboard.name}
              </option>
            ))}
          </select>
          <button className="primary-button" type="button" onClick={saveLayout} disabled={!canEditDashboard}>
            <Save size={18} />
            {t("dashboard.saveLayout")}
          </button>
        </div>
      </div>

      <section className="dashboard-builder">
        <aside className="widget-library">
          <div className="panel-title">
            <Library size={18} />
            <strong>{t("dashboard.widgetLibrary")}</strong>
          </div>
          <div className="library-list">
            {widgetRegistry.map((widget) => (
              <button key={widget.type} type="button" onClick={() => addWidget(widget.type)} disabled={!canEditDashboard}>
                <Plus size={16} />
                <span>
                  <strong>{t(widget.titleKey)}</strong>
                  <small>{t(widget.categoryKey)} · {t(widget.descriptionKey)}</small>
                </span>
              </button>
            ))}
          </div>
          <div className="new-dashboard">
            <input
              value={newDashboardName}
              onChange={(event) => setNewDashboardName(event.target.value)}
              placeholder={t("dashboard.dashboardName")}
              disabled={!canEditDashboard}
            />
            <button type="button" onClick={createDashboard} disabled={!canEditDashboard || !newDashboardName.trim()}>
              <Plus size={16} />
              {t("dashboard.newDashboard")}
            </button>
          </div>
        </aside>

        <div className="dashboard-grid editable">
          {widgets.map((layout) => {
            const definition = registryByType.get(layout.type);
            if (!definition) {
              return null;
            }

            return (
              <article
                key={layout.id}
                className={`widget col-${layout.width} row-${layout.height}`}
                draggable={canEditDashboard}
                onDragStart={() => setDraggedWidgetId(layout.id)}
                onDragOver={(event) => event.preventDefault()}
                onDrop={() => moveWidget(layout.id)}
              >
                <header>
                  <span className="drag-handle" aria-hidden="true">
                    <GripVertical size={16} />
                  </span>
                  <strong>{t(definition.titleKey)}</strong>
                  <div className="widget-tools">
                    <button type="button" onClick={() => resizeWidget(layout.id, "width", -1)} disabled={!canEditDashboard}>
                      W-
                    </button>
                    <button type="button" onClick={() => resizeWidget(layout.id, "width", 1)} disabled={!canEditDashboard}>
                      W+
                    </button>
                    <button type="button" onClick={() => resizeWidget(layout.id, "height", -1)} disabled={!canEditDashboard}>
                      H-
                    </button>
                    <button type="button" onClick={() => resizeWidget(layout.id, "height", 1)} disabled={!canEditDashboard}>
                      H+
                    </button>
                    <button type="button" aria-label="Move widget left" onClick={() => moveWidgetByDirection(layout.id, -1)} disabled={!canEditDashboard}>
                      <ArrowLeft size={14} />
                    </button>
                    <button type="button" aria-label="Move widget right" onClick={() => moveWidgetByDirection(layout.id, 1)} disabled={!canEditDashboard}>
                      <ArrowRight size={14} />
                    </button>
                    <button type="button" aria-label="Remove widget" onClick={() => removeWidget(layout.id)} disabled={!canEditDashboard}>
                      <Trash2 size={14} />
                    </button>
                  </div>
                </header>
                {definition.render({ width: layout.width, height: layout.height, config: layout.config })}
              </article>
            );
          })}
        </div>
      </section>

      <section className="layout-editor">
        <div>
          <h2>{t("dashboard.layoutJson")}</h2>
          <p>{canEditDashboard ? status : t("dashboard.readOnly")}</p>
        </div>
        <textarea
          value={rawJson}
          onChange={(event) => setRawJson(event.target.value)}
          spellCheck={false}
          disabled={!canEditDashboard}
        />
        <button type="button" onClick={applyJson} disabled={!canEditDashboard}>
          {t("dashboard.applyJson")}
        </button>
      </section>
    </section>
  );
}
