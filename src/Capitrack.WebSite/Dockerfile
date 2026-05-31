# Capitrack marketing + documentation site (capitrack.dev).
# Pure static site served by nginx. Build context is this folder.
FROM nginx:alpine

# Site config (SPA-ish clean URLs for the docs pages).
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Static site → web root.
COPY index.html /usr/share/nginx/html/index.html
COPY docs/      /usr/share/nginx/html/docs/
COPY styles/    /usr/share/nginx/html/styles/
COPY js/        /usr/share/nginx/html/js/
COPY assets/    /usr/share/nginx/html/assets/

EXPOSE 80
