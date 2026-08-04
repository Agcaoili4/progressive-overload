import { createContext, useCallback, useContext, useEffect, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { api, ApiError } from '../api/client';
import type { AuthResponse, Profile } from '../api/client';

type AuthState = {
  profile: Profile | null;
  loading: boolean;
  register: (email: string, password: string, displayName: string) => Promise<void>;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  token: () => string | null;
  refreshProfile: () => Promise<void>;
};

const Ctx = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  /*
      A ref, not state: the access token must never land in localStorage, and keeping it out
      of React state also keeps it out of devtools component trees and any future
      serialisation of state. Re-renders are driven by `profile` instead.
  */
  const accessToken = useRef<string | null>(null);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [loading, setLoading] = useState(true);

  const adopt = useCallback(async (auth: AuthResponse) => {
    accessToken.current = auth.accessToken;
    setProfile(await api.me(auth.accessToken));
  }, []);

  /*
      On boot the access token is gone — it only ever lived in memory — but the refresh
      cookie may still be valid, so a silent refresh restores the session across a reload.
      A 401 here is the normal "not signed in" case, not an error worth surfacing.
  */
  useEffect(() => {
    (async () => {
      try {
        await adopt(await api.refresh());
      } catch (e) {
        if (!(e instanceof ApiError)) console.error('refresh failed', e);
      } finally {
        setLoading(false);
      }
    })();
  }, [adopt]);

  const value: AuthState = {
    profile,
    loading,
    register: async (email, password, displayName) =>
      adopt(await api.register(email, password, displayName)),
    login: async (email, password) => adopt(await api.login(email, password)),
    logout: async () => {
      await api.logout();
      accessToken.current = null;
      setProfile(null);
    },
    token: () => accessToken.current,
    refreshProfile: async () => {
      if (accessToken.current) setProfile(await api.me(accessToken.current));
    },
  };

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useAuth() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
