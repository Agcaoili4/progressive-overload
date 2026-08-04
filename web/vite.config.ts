import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import tailwindcss from '@tailwindcss/vite';
import fs from 'node:fs';

/*
    HTTPS in development is not optional here. The refresh cookie is Secure, and WebKit does
    not treat localhost as a secure context, so Safari silently refuses to store it over
    http. Chrome would tolerate http, but only until the API is also https — Chrome enforces
    schemeful same-site, so a mixed http/https pair breaks SameSite=Strict instead.
    Both ends therefore run https, sharing the .NET dev certificate so neither warns.
*/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    https: {
      key: fs.readFileSync('./certs/localhost.key'),
      cert: fs.readFileSync('./certs/localhost.pem'),
    },
  },
});
