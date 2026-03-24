/** Production build — replaced via `fileReplacements` in `project.json`. */
export const environment = {
  production: true,
  /** Same host as the portal (Coolify / reverse proxy routes /api to the ERP.Api container). */
  apiBaseUrl: 'https://admissionsdbctura.com/api',
};
