const API_BASE_URL = window.EKIPZEN_API_BASE_URL || "https://localhost:7001/api";

const getAuthHeaders = () => {
  const session = getCurrentSession?.();
  return session?.token ? { Authorization: `Bearer ${session.token}` } : {};
};

const apiRequest = async (path, options = {}) => {
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      "Content-Type": "application/json",
      ...getAuthHeaders(),
      ...(options.headers || {}),
    },
  });

  if (!response.ok) {
    throw new Error(`API request failed: ${response.status}`);
  }

  if (response.status === 204) return null;
  return response.json();
};

const apiGet = (path) => apiRequest(path);
const apiPost = (path, body) => apiRequest(path, { method: "POST", body: JSON.stringify(body) });
const apiPut = (path, body) => apiRequest(path, { method: "PUT", body: JSON.stringify(body) });
const apiDelete = (path) => apiRequest(path, { method: "DELETE" });

/*
  .NET 9 backend contract placeholder:
  POST  /api/auth/login
  POST  /api/auth/register
  GET   /api/me
  GET   /api/actions
  GET   /api/audit-logs
  PATCH /api/actions/{id}/status
*/
