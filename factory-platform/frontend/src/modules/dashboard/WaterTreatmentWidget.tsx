import { Droplets, RefreshCw, ShieldAlert, Waves } from "lucide-react";
import { useEffect, useMemo, useState } from "react";
import { apiRequest } from "../../shared/api/client";
import type { WaterTreatmentMetric, WaterTreatmentWidgetData } from "../../shared/api/types";
import { useAuth } from "../../shared/hooks/useAuth";
import { useLocalization } from "../../shared/i18n/LocalizationProvider";

export type WaterTreatmentWidgetConfig = {
  area: string;
  refreshSeconds: number;
  tags: {
    rawTank1Level: string;
    rawTank2Level: string;
    roTankLevel: string;
    softTankLevel: string;
    feedPump1Status: string;
    feedPump2Status: string;
    roPressure: string;
    roConductivity: string;
    roFlow: string;
    softWaterFlow: string;
    activeAlarms: string;
  };
};

export const defaultWaterTreatmentWidgetConfig: WaterTreatmentWidgetConfig = {
  area: "WaterTreatment",
  refreshSeconds: 5,
  tags: {
    rawTank1Level: "WT_RAW_TANK_1_LEVEL",
    rawTank2Level: "WT_RAW_TANK_2_LEVEL",
    roTankLevel: "WT_RO_TANK_LEVEL",
    softTankLevel: "WT_SOFT_TANK_LEVEL",
    feedPump1Status: "WT_FEED_PUMP_1_STATUS",
    feedPump2Status: "WT_FEED_PUMP_2_STATUS",
    roPressure: "WT_RO_PRESSURE",
    roConductivity: "WT_RO_CONDUCTIVITY",
    roFlow: "WT_RO_FLOW",
    softWaterFlow: "WT_SOFT_WATER_FLOW",
    activeAlarms: "WT_ACTIVE_ALARMS"
  }
};

type WidgetSize = {
  width?: 1 | 2 | 4;
  height?: 1 | 2;
};

export function WaterTreatmentWidget({ config, size }: { config?: Partial<WaterTreatmentWidgetConfig>; size?: WidgetSize }) {
  const { token } = useAuth();
  const { t } = useLocalization();
  const [data, setData] = useState<WaterTreatmentWidgetData | null>(null);
  const [status, setStatus] = useState(t("water.loading"));
  const widgetConfig = useMemo(() => ({ ...defaultWaterTreatmentWidgetConfig, ...config }), [config]);
  const compact = (size?.width ?? 2) <= 1 || (size?.height ?? 1) <= 1;

  useEffect(() => {
    if (!token) return;

    let isMounted = true;
    async function load() {
      try {
        const result = await apiRequest<WaterTreatmentWidgetData>("/widgets/water-treatment", { token });
        if (isMounted) {
          setData(result);
          setStatus(t("water.updated", { time: formatTime(result.lastUpdated) }));
        }
      } catch (error) {
        if (isMounted) {
          setStatus(error instanceof Error ? error.message : "Could not load water treatment data.");
        }
      }
    }

    void load();
    const interval = window.setInterval(() => void load(), Math.max(2, widgetConfig.refreshSeconds) * 1000);
    return () => {
      isMounted = false;
      window.clearInterval(interval);
    };
  }, [token, widgetConfig.refreshSeconds]);

  if (!data) {
    return (
      <div className="water-widget loading">
        <RefreshCw size={20} />
          <span>{status}</span>
      </div>
    );
  }

  const metrics = [
    data.feedPump1Status,
    data.feedPump2Status,
    data.roPressure,
    data.roConductivity,
    data.roFlow,
    data.softWaterFlow
  ];

  return (
    <div className={`water-widget ${compact ? "compact" : "expanded"}`}>
      <div className="water-widget-summary">
        <div>
          <span>{t("water.area")}</span>
          <strong>{widgetConfig.area}</strong>
        </div>
        <div className={Number(data.activeAlarms.value) > 0 ? "alarm-active" : ""}>
          <ShieldAlert size={16} />
          <strong>{data.activeAlarms.value}</strong>
          <span>{t("water.alarms")}</span>
        </div>
        <small>{status}</small>
      </div>

      <div className="water-tanks">
        {data.tanks.map((tank) => (
          <div className="water-tank-card" key={tank.tagCode} title={tank.tagCode}>
            <div className="tank-graphic">
              <div style={{ height: `${Math.max(0, Math.min(100, tank.level))}%` }} />
            </div>
            <div>
              <strong>{translateTankName(tank.name, t)}</strong>
              <span>{tank.level.toFixed(1)} {tank.unit}</span>
              <small>{translateTankStatus(tank.status, t)} · {formatTime(tank.lastUpdated)}</small>
            </div>
          </div>
        ))}
      </div>

      <div className="water-metrics">
        {metrics.map((metric) => (
          <MetricTile key={metric.tagCode} metric={metric} />
        ))}
      </div>
    </div>
  );
}

function MetricTile({ metric }: { metric: WaterTreatmentMetric }) {
  const { t } = useLocalization();

  return (
    <div className={`water-metric quality-${metric.quality.toLowerCase()}`} title={metric.tagCode}>
      {metric.label.includes("Flow") ? <Waves size={16} /> : <Droplets size={16} />}
      <span>{translateMetricLabel(metric.label, t)}</span>
      <strong>{metric.value}{metric.unit ? ` ${metric.unit}` : ""}</strong>
    </div>
  );
}

function translateTankName(name: string, t: (key: string) => string) {
  const keyByName: Record<string, string> = {
    "Raw Water Tank 1": "tank.raw1",
    "Raw Water Tank 2": "tank.raw2",
    "RO Water Tank": "tank.ro",
    "Soft Water Tank": "tank.soft"
  };
  return keyByName[name] ? t(keyByName[name]) : name;
}

function translateTankStatus(status: string, t: (key: string) => string) {
  return status === "Normal" ? t("water.normal") : status === "Check" ? t("water.check") : status;
}

function translateMetricLabel(label: string, t: (key: string) => string) {
  const keyByLabel: Record<string, string> = {
    "Feed Pump 1": "water.feedPump1",
    "Feed Pump 2": "water.feedPump2",
    "RO Pressure": "water.roPressure",
    "RO Conductivity": "water.roConductivity",
    "RO Flow": "water.roFlow",
    "Soft Water Flow": "water.softWaterFlow",
    "Active Alarms": "water.activeAlarms"
  };
  return keyByLabel[label] ? t(keyByLabel[label]) : label;
}

function formatTime(value: string) {
  return new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit", second: "2-digit" });
}
