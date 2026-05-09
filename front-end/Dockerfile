# ==========================================================================
# MULTI-STAGE PRODUCTION DOCKERFILE — MIMIC AI FRONTEND
# ==========================================================================

# --- STAGE 1: Build Environment ---
FROM node:20-alpine AS build
WORKDIR /app

# Copy dependency specifications and install cleanly
COPY package*.json ./
RUN npm install

# Copy full source and compile production assets via Vite
COPY . .
RUN npm run build

# --- STAGE 2: Lightweight Serve Environment ---
FROM nginx:alpine

# Copy built SPA files from the compile stage to Nginx web root
COPY --from=build /app/dist /usr/share/nginx/html

# Expose Nginx server port
EXPOSE 80

# Start Nginx in foreground
CMD ["nginx", "-g", "daemon off;"]
