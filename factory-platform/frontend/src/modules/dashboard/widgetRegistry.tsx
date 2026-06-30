import { AlertTriangle, BarChart2, Table2, TrendingUp } from "lucide-react";
import type { ReactNode } from "react";

export type WidgetDefinition = {
  type: string;
  title: string;
  defaultWidth: 1 | 2 | 4;
  defaultHeight: 1 | 2;
  render: () => ReactNode;
};

export const widgetRegistry: WidgetDefinition[] = [
  {
    type: "kpi-card",
    title: "KPI Card",
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
    defaultWidth: 1,
    defaultHeight: 1,
    render: () => (
      <div className="widget-content alarm">
        <AlertTriangle size={26} />
        <strong>0</strong>
        <span>Active alarms placeholder</span>
      </div>
    )
  }
];
