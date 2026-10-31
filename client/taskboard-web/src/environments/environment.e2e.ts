export const environment = {
  // The Playwright run starts its own API on a separate port (and database), so the e2e tests
  // never touch the dev API on 5063 and can run while the dev servers are up.
  apiUrl: 'http://localhost:5180'
};
