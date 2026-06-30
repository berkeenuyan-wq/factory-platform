export function UnderDevelopmentPage({ moduleName }: { moduleName: string }) {
  return (
    <section className="module-page">
      <p className="eyebrow">{moduleName}</p>
      <h1>{moduleName}</h1>
      <div className="empty-state">This module is under development.</div>
    </section>
  );
}
