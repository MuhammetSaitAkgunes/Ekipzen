import { RouterProvider, createBrowserRouter } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { AuthProvider } from "./auth/AuthContext";
import { ToastProvider } from "./layout/ToastProvider";
import { createQueryClient } from "./queryClient";
import { buildRouteObjects } from "./routes";
import { legacyHashTarget } from "./legacyHash";

// Hash router döneminden kalan bağlantılar (ör. /#/accept-invite?…) router kurulmadan çevrilir.
const legacyTarget = legacyHashTarget(window.location.hash);
if (legacyTarget) {
  try {
    window.history.replaceState(null, "", legacyTarget);
  } catch {
    // Beklenmeyen/işlenemeyen bir hash biçimi modül yüklenmesini bloklamamalı.
  }
}

const router = createBrowserRouter(buildRouteObjects());
const queryClient = createQueryClient();

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ToastProvider>
          <RouterProvider router={router} />
        </ToastProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
