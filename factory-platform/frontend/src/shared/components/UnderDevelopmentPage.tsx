import { useLocalization } from "../i18n/LocalizationProvider";

export function UnderDevelopmentPage({ moduleNameKey }: { moduleNameKey: string }) {
  const { t } = useLocalization();
  const moduleName = t(moduleNameKey);

  return (
    <section className="module-page">
      <p className="eyebrow">{moduleName}</p>
      <h1>{moduleName}</h1>
      <div className="empty-state">{t("module.underDevelopment")}</div>
    </section>
  );
}
