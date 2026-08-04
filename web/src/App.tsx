import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider, useAuth } from './auth/AuthContext';
import { SessionProvider } from './session/SessionContext';
import SignIn from './screens/SignIn';
import Today from './screens/Today';
import ExerciseScreen from './screens/ExerciseScreen';

function Gate() {
  const { profile, loading } = useAuth();

  // The silent refresh on boot decides this; rendering either screen early would flash.
  if (loading) return <div className="flex h-[100dvh] items-center justify-center text-white/30">…</div>;
  if (!profile) return <SignIn />;

  return (
    <SessionProvider>
      <Routes>
        <Route path="/" element={<Today />} />
        <Route path="/session" element={<ExerciseScreen />} />
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
