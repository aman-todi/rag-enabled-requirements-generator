import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Builds directly into the ASP.NET Core project's wwwroot/ so `dotnet publish` (which runs
// `npm run build` via Server.csproj's BuildClient target) produces one self-contained artifact.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: "../Server/wwwroot",
    emptyOutDir: true,
  },
  server: {
    // During `npm run dev`, proxy API calls to the locally running ASP.NET Core backend
    // (see src/Server/Properties/launchSettings.json) so the SPA and API share an origin
    // the same way they do in production, avoiding CORS.
    proxy: {
      "/api": {
        target: process.env.VITE_API_PROXY_TARGET ?? "http://localhost:5080",
        changeOrigin: true,
      },
    },
  },
});
