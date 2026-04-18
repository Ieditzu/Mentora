// In development, we use the Vite proxy (relative paths).
// In production, we can still use an environment variable if needed.
const API_BASE = import.meta.env.VITE_API_BASE || ""; 

export async function api(path, options = {}, token = "") {
  // Ensure the path starts with /
  const sanitizedPath = path.startsWith('/') ? path : `/${path}`;
  const url = `${API_BASE}${sanitizedPath}`;
  
  const headers = {
    "Content-Type": "application/json",
    ...(options.headers || {})
  };
  
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }

  try {
    const response = await fetch(url, { 
      ...options, 
      headers
      // mode: 'cors' and credentials are no longer needed for proxy
    });
    
    const data = await response.json().catch(() => ({}));
    
    if (!response.ok || data.error) {
      throw new Error(data.error || `Request failed with status ${response.status}`);
    }
    return data;
  } catch (err) {
    console.error("API Call Failed:", err);
    throw err;
  }
}

export { API_BASE };
