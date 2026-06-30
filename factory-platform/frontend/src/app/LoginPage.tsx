import { FormEvent, useState } from "react";
import { Factory, LockKeyhole } from "lucide-react";
import { useAuth } from "../shared/hooks/useAuth";
import { useLocalization } from "../shared/i18n/LocalizationProvider";

export function LoginPage() {
  const { login } = useAuth();
  const { t } = useLocalization();
  const [email, setEmail] = useState("admin@factory.local");
  const [password, setPassword] = useState("Admin123!");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);
    try {
      await login(email, password);
    } catch (err) {
      setError(err instanceof Error ? err.message : t("login.failed"));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <main className="login-page">
      <section className="login-panel">
        <div className="brand-mark">
          <Factory size={30} />
        </div>
        <div>
          <p className="eyebrow">{t("login.eyebrow")}</p>
          <h1>{t("login.title")}</h1>
        </div>
        <form onSubmit={handleSubmit} className="login-form">
          <label>
            {t("login.email")}
            <input value={email} onChange={(event) => setEmail(event.target.value)} type="email" />
          </label>
          <label>
            {t("login.password")}
            <input value={password} onChange={(event) => setPassword(event.target.value)} type="password" />
          </label>
          {error && <p className="form-error">{error}</p>}
          <button type="submit" disabled={isSubmitting}>
            <LockKeyhole size={18} />
            {isSubmitting ? t("login.signingIn") : t("login.signIn")}
          </button>
        </form>
      </section>
    </main>
  );
}
