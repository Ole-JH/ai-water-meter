# The pre-deploy browser check: headless Chromium (Playwright) playing the new build against a throwaway server.
# Keep the image tag and the playwright version in package.json the same.
FROM mcr.microsoft.com/playwright:v1.56.1-noble
WORKDIR /check
COPY package.json ./
RUN npm install --omit=dev --no-audit --no-fund && npm cache clean --force
COPY check.js ./
ENV HOME=/tmp
CMD ["node", "check.js"]
