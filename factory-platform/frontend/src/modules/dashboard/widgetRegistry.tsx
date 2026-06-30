import { AlertTriangle, BarChart2, Table2, TrendingUp } from "lucide-react";
import type { ReactNode } from "react";
import { defaultWaterTreatmentWidgetConfig, WaterTreatmentWidget } from "./WaterTreatmentWidget";

export type WidgetLayoutContext = {
  width?: 1 | 2 | 4;
  height?: 1 | 2;
  config?: unknown;
};

export type WidgetDefinition = {
  type: string;
  title: string;
  titleKey: string;
  category: string;
  categoryKey: string;
  description: string;
  descriptionKey: string;
  defaultWidth: 1 | 2 | 4;
  defaultHeight: 1 | 2;
  defaultConfig?: unknown;
  render: (context?: WidgetLayoutContext) => ReactNode;
};

export const widgetRegistry: WidgetDefinition[] = [
  {
    type: "kpi-card",
    title: "KPI Card",
    titleKey: "widget.kpi",
    category: "Core",
    categoryKey: "widget.category.core",
    description: "Placeholder KPI value for early dashboard layouts.",
    descriptionKey: "widget.kpi.description",
    defaultWidth: 1,
    defaultHeight: 1,
    render: () => (
      <div className="widget-content">
        <TrendingUp size={26} />
        <strong>84.2%</strong>
        <span>Placeholder OEE</span>
      </div>
    )
  },
  {
    type: "chart-placeholder",
    title: "Chart Placeholder",
    titleKey: "widget.chart",
    category: "Core",
    categoryKey: "widget.category.core",
    description: "Placeholder chart area for future process and production trends.",
    descriptionKey: "widget.chart.description",
    defaultWidth: 2,
    defaultHeight: 1,
    render: () => (
      <div className="widget-content">
        <BarChart2 size={26} />
        <strong>Chart Placeholder</strong>
        <span>Future trends and production signals.</span>
      </div>
    )
  },
  {
    type: "table-placeholder",
    title: "Table Placeholder",
    titleKey: "widget.table",
    category: "Core",
    categoryKey: "widget.category.core",
    description: "Placeholder table for future operational records.",
    descriptionKey: "widget.table.description",
    defaultWidth: 4,
    defaultHeight: 1,
    render: () => (
      <div className="widget-content">
        <Table2 size={26} />
        <strong>Table Placeholder</strong>
        <span>Future work orders, assets, or quality records.</span>
      </div>
    )
  },
  {
    type: "alarm-placeholder",
    title: "Alarm Placeholder",
    titleKey: "widget.alarm",
    category: "Core",
    categoryKey: "widget.category.core",
    description: "Placeholder active alarm count.",
    descriptionKey: "widget.alarm.description",
    defaultWidth: 1,
    defaultHeight: 1,
    render: () => (
      <div className="widget-content alarm">
        <AlertTriangle size={26} />
        <strong>0</strong>
        <span>Active alarms placeholder</span>
      </div>
    )
  },
  {
    type: "water-treatment-overview",
    title: "Water Treatment Overview",
    titleKey: "widget.water",
    category: "Utilities",
    categoryKey: "widget.category.utilities",
    description: "Shows tank levels, pump status, RO conductivity, flow, pressure, and active alarms for the water treatment area.",
    descriptionKey: "widget.water.description",
    defaultWidth: 4,
    defaultHeight: 2,
    defaultConfig: defaultWaterTreatmentWidgetConfig,
    render: (context) => (
      <WaterTreatmentWidget
        config={context?.config as Partial<typeof defaultWaterTreatmentWidgetConfig> | undefined}
        size={{ width: context?.width, height: context?.height }}
      />
    )
  }
];
