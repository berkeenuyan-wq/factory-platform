import { createContext, ReactNode, useContext, useMemo, useState } from "react";
import { LanguageCode, translations } from "./translations";

type LocalizationContextValue = {
  language: LanguageCode;
  setLanguage: (language: LanguageCode) => void;
  t: (key: string, values?: Record<string, string | number>) => string;
};

const LocalizationContext = createContext<LocalizationContextValue | undefined>(undefined);
const languageStorageKey = "factoryos-language";

export function LocalizationProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState<LanguageCode>(() => {
    const stored = localStorage.getItem(languageStorageKey);
    return stored === "en" || stored === "tr" ? stored : "tr";
  });

  const value = useMemo<LocalizationContextValue>(() => ({
    language,
    setLanguage: (nextLanguage) => {
      localStorage.setItem(languageStorageKey, nextLanguage);
      setLanguageState(nextLanguage);
    },
    t: (key, values = {}) => {
      const template = translations[language][key] ?? translations.tr[key] ?? translations.en[key] ?? key;
      return Object.entries(values).reduce(
        (text, [name, replacement]) => text.split(`{${name}}`).join(String(replacement)),
        template
      );
    }
  }), [language]);

  return <LocalizationContext.Provider value={value}>{children}</LocalizationContext.Provider>;
}

export function useLocalization() {
  const value = useContext(LocalizationContext);
  if (!value) {
    throw new Error("useLocalization must be used inside LocalizationProvider");
  }
  return value;
}
