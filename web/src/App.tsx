import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth/AuthContext';
import { SessionProvider } from './session/SessionContext';
import SignIn from './screens/SignIn';
import Onboarding from './screens/Onboarding';
import Privacy from './screens/Privacy';
import Today from './screens/Today';
import ExerciseScreen from './screens/ExerciseScreen';

function Gate() {
  const { profile, loading, needsOnboarding } = useAuth();

  // The silent refresh on boot decides this; rendering either branch early would flash.
  if (loading) return <div className="flex h-[100dvh] items-center justify-center text-white/30">…</div>;

  if (!profile) {
    /* Reachable while signed out, since it is linked from the sign-in screen. */
    return (
      <Routes>
        <Route path="/privacy" element={<Privacy />} />
        <Route path="*" element={<SignIn />} />
      </Routes>
    );
  }

  if (needsOnboarding) return <Onboarding />;

  return (
    <SessionProvider>
      <Routes>
        <Route path="/" element={<Today />} />
        <Route path="/session" element={<ExerciseScreen />} />
        <Route path="/privacy" element={<Privacy />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </SessionProvider>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider><Gate /></AuthProvider>
    </BrowserRouter>
  );
}
