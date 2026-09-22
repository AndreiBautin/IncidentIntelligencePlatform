FROM node:20-alpine AS deps
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm install

FROM node:20-alpine AS builder
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY . .
ENV NEXT_TELEMETRY_DISABLED=1
ARG NEXT_PUBLIC_API_URL=http://api:8080
ENV NEXT_PUBLIC_API_URL=$NEXT_PUBLIC_API_URL
ARG NEXT_PUBLIC_READ_ONLY=false
ENV NEXT_PUBLIC_READ_ONLY=$NEXT_PUBLIC_READ_ONLY
RUN npm run build

FROM node:20-alpine AS runner
WORKDIR /app
ENV NODE_ENV=production
ENV NEXT_TELEMETRY_DISABLED=1
RUN addgroup --system --gid 1001 nodejs
RUN adduser --system --uid 1001 nextjs
COPY --from=builder /app/public ./public
COPY --from=builder --chown=nextjs:nodejs /app/.next/standalone ./
COPY --from=builder --chown=nextjs:nodejs /app/.next/static ./.next/static
USER nextjs
EXPOSE 3000
ENV PORT=3000
# Next.js standalone's server.js reads HOSTNAME to decide what interface to bind to. Docker (and
# Render) sets HOSTNAME to the container's own hostname by default, which makes the server bind to
# that single interface instead of all of them - the app comes up and logs "Ready" while staying
# unreachable from outside the container, which shows up as a 502 at the platform's edge rather
# than any error the app itself ever sees.
ENV HOSTNAME=0.0.0.0
CMD ["node", "server.js"]
