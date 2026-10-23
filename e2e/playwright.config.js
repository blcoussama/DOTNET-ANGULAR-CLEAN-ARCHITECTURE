// Tests navigateur de bout en bout (Chromium) : frontend Angular -> API ASP.NET Core -> base de données.
// Prérequis : la pile lancée par ./run-stack.sh (voir README racine, section « Tests »).
module.exports = {
  testDir: __dirname,
  testMatch: /\.spec\.js$/,
  workers: 1,
  retries: 0,
  timeout: 90_000,
  expect: { timeout: 10_000 },
  reporter: [['list']],
  use: { headless: true, screenshot: 'only-on-failure', trace: 'retain-on-failure', locale: 'fr-FR' },
  outputDir: __dirname + '/.tmp/results',
};
