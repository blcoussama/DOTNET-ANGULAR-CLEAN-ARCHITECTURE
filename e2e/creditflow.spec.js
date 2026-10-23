// Parcours de bout en bout dans un vrai navigateur : Angular -> API ASP.NET Core -> base réelle.
// Prérequis : la pile lancée par ./run-stack.sh (comptes de démo semés avec E2E_SEED_PASSWORD).
const { test, expect } = require('@playwright/test');

const BASE = process.env.E2E_BASE_URL || 'http://localhost:4200';
const DEMO_PASSWORD = process.env.E2E_SEED_PASSWORD || 'E2e-Demo-2026!';
const ANALYST = 'analyste@creditflow.local';
const ADMIN = 'admin@creditflow.local';
const PASSWORD = 'Client-E2e-2026!';

// Fichiers minimaux mais valides (signatures binaires contrôlées côté client ET côté serveur)
const PDF = Buffer.from('%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF\n');
const PNG = Buffer.from(
  '89504e470d0a1a0a0000000d4948445200000001000000010806000000' +
    '1f15c4890000000d49444154789c6360000002000154a24f5d0000000049454e44ae426082',
  'hex',
);
const FAKE_PDF = Buffer.from('ceci est un simple texte renommé en .pdf');

const unique = () => `${Date.now()}${Math.floor(Math.random() * 1000)}`;

async function login(page, email, password) {
  await page.goto(`${BASE}/connexion`);
  await page.getByLabel('Adresse e-mail').fill(email);
  await page.getByLabel('Mot de passe', { exact: true }).fill(password);
  await page.getByRole('button', { name: 'Se connecter' }).click();
  await expect(page.getByRole('button', { name: 'Menu utilisateur' })).toBeVisible();
}

async function upload(page, type, name, mimeType, buffer) {
  await page.getByTestId(`file-input-${type}`).setInputFiles({ name, mimeType, buffer });
  // Envoi terminé quand la pièce apparaît dans la liste (bouton de téléchargement)
  await expect(page.locator(`section[aria-labelledby="slot-${type}"]`).getByRole('button', { name: `Télécharger ${name}` })).toBeVisible();
}

async function assign(page) {
  await page.getByTestId('assign').click();
  await page.getByRole('dialog').getByRole('button', { name: /assigner le dossier/ }).click();
  await expect(page.getByTestId('status')).toHaveText('En cours d\'étude');
}

async function decide(page, action, comment) {
  await page.getByTestId(action).click();
  await page.getByTestId('decision-comment').fill(comment);
  await page.getByTestId('decision-confirm').click();
}

test('simulateur public : la mensualité est calculée par l\'API', async ({ page }) => {
  await page.goto(BASE);
  await expect(page.getByRole('heading', { name: 'Simulateur de crédit' })).toBeVisible();
  // 50 000 MAD sur 36 mois à 7,5 % : mensualité de l'annuité = 1 555,31 MAD
  await expect(page.getByText(/1\s555,31\sMAD/).first()).toBeVisible();
});

test('parcours complet : demande -> compléments -> resoumission -> approbation', async ({ browser }) => {
  const clientCtx = await browser.newContext({ locale: 'fr-FR' });
  const client = await clientCtx.newPage();
  const email = `client.${unique()}@e2e.test`;

  // 1. Inscription (connexion automatique) puis profil financier
  await client.goto(`${BASE}/inscription`);
  await client.getByLabel('Prénom').fill('Karim');
  await client.getByLabel('Nom', { exact: true }).fill('Testeur');
  await client.getByLabel('Adresse e-mail').fill(email);
  await client.getByLabel('Mot de passe', { exact: true }).fill(PASSWORD);
  await client.getByLabel('Confirmer le mot de passe').fill(PASSWORD);
  await client.getByRole('button', { name: 'Créer mon compte' }).click();
  await expect(client).toHaveURL(/\/client\/profil/);

  await client.getByLabel('Date de naissance').fill('1992-05-14');
  await client.getByLabel('Téléphone').fill('+212600112233');
  await client.getByLabel('Ville').fill('Tanger');
  await client.getByLabel('Type de contrat').click();
  await client.getByRole('option', { name: 'CDI' }).click();
  await client.getByLabel(/Ancienneté/).fill('4');
  await client.getByLabel('Revenu mensuel net (MAD)').fill('15000');
  await client.getByLabel('Mensualités de crédits en cours (MAD)').fill('1000');
  await client.getByRole('button', { name: 'Enregistrer le profil' }).click();
  await expect(client.getByTestId('profile-complete')).toBeVisible();

  // 2. Nouvelle demande (stepper) : produit, montant / durée, pièces, récapitulatif
  await client.getByRole('link', { name: 'Nouvelle demande' }).first().click();
  await client.getByTestId('product-AUTO').click();
  await client.getByRole('button', { name: 'Continuer' }).first().click();
  await client.getByLabel('Montant souhaité (MAD)').fill('150000');
  await client.getByLabel('Durée (mois)').fill('60');
  await client.getByLabel('Objet du financement').fill('Achat d\'un véhicule neuf pour le travail');
  await client.getByTestId('save-draft').click();

  // Une pièce falsifiée (texte renommé en .pdf) est refusée avant tout envoi
  await client.getByTestId('file-input-ID_CARD').setInputFiles({ name: 'faux.pdf', mimeType: 'application/pdf', buffer: FAKE_PDF });
  await expect(client.locator('section[aria-labelledby="slot-ID_CARD"]').getByRole('alert')).toBeVisible();
  await upload(client, 'ID_CARD', 'cin.pdf', 'application/pdf', PDF);
  await upload(client, 'PAYSLIP', 'bulletin.png', 'image/png', PNG);
  await client.getByRole('button', { name: 'Continuer' }).last().click();

  // Récapitulatif : mensualité de 150 000 MAD / 60 mois à 6,5 % = 2 934,92 MAD
  await expect(client.getByTestId('recap-payment')).toHaveText(/2\s934,92\sMAD/);
  await client.getByTestId('certify').click();
  await client.getByTestId('submit-application').click();
  await expect(client).toHaveURL(/\/client\/demandes\/[0-9a-f-]{36}$/);
  await expect(client.getByTestId('status')).toHaveText('Soumise');
  const reference = (await client.getByText(/CF-\d{4}-\d{6}/).first().textContent()).match(/CF-\d{4}-\d{6}/)[0];
  await expect(client.getByTestId('frozen-terms')).toContainText('2 934,92');

  // 3. L'analyste retrouve le dossier, se l'assigne et demande des compléments
  const analystCtx = await browser.newContext({ locale: 'fr-FR' });
  const analyst = await analystCtx.newPage();
  await login(analyst, ANALYST, DEMO_PASSWORD);
  await expect(analyst).toHaveURL(/\/analyste\/file/);
  await analyst.getByTestId('queue-search').fill(reference);
  // Attendre le résultat filtré (recherche côté serveur, temporisée) avant d'ouvrir le dossier
  await expect(analyst.getByRole('table', { name: 'Demandes à instruire' }).getByRole('row')).toHaveCount(2);
  await analyst.getByRole('link', { name: reference }).click();
  await expect(analyst).toHaveURL(/\/analyste\/dossiers\//);
  await expect(analyst.getByLabel('Décomposition du score')).toBeVisible();
  await expect(analyst.getByTestId('criterion')).toHaveCount(4);
  await expect(analyst.getByTestId('client-profile')).toContainText('Tanger');

  // Téléchargement d'une pièce par l'analyste (jeton envoyé par HttpClient)
  const downloadPromise = analyst.waitForEvent('download');
  await analyst.getByRole('button', { name: /Télécharger cin\.pdf/ }).click();
  expect((await downloadPromise).suggestedFilename()).toBe('cin.pdf');

  await assign(analyst);
  await decide(analyst, 'request-info', 'Merci de joindre un relevé bancaire des trois derniers mois.');
  await expect(analyst.getByTestId('status')).toHaveText('Compléments demandés');

  // 4. Le client voit la demande de compléments, dépose la pièce et renvoie son dossier
  await client.reload();
  await expect(client.getByTestId('info-request')).toContainText('relevé bancaire');
  await upload(client, 'BANK_STATEMENT', 'releve.pdf', 'application/pdf', PDF);
  await client.getByTestId('resubmit').click();
  await client.getByRole('dialog').getByRole('button', { name: 'Renvoyer' }).click();
  await expect(client.getByTestId('status')).toHaveText('Soumise');

  // 5. L'analyste reprend le dossier et l'approuve
  await analyst.reload();
  await expect(analyst.getByTestId('status')).toHaveText('Soumise');
  await assign(analyst);
  await decide(analyst, 'approve', 'Dossier complet, capacité de remboursement confirmée.');
  await expect(analyst.getByTestId('status')).toHaveText('Approuvée');

  // 6. Le client voit la décision motivée et l'historique complet
  await client.reload();
  await expect(client.getByTestId('status')).toHaveText('Approuvée');
  await expect(client.getByText('capacité de remboursement confirmée').first()).toBeVisible();

  // Le jeton d'accès n'est jamais stocké dans le navigateur
  const stored = await client.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }));
  expect(stored).not.toMatch(/eyJ/);

  await clientCtx.close();
  await analystCtx.close();
});

test('contrôle d\'accès : session restaurée par le cookie, pages interdites, déconnexion', async ({ page }) => {
  const email = `client.${unique()}@e2e.test`;
  await page.goto(`${BASE}/inscription`);
  await page.getByLabel('Prénom').fill('Nadia');
  await page.getByLabel('Nom', { exact: true }).fill('Acces');
  await page.getByLabel('Adresse e-mail').fill(email);
  await page.getByLabel('Mot de passe', { exact: true }).fill(PASSWORD);
  await page.getByLabel('Confirmer le mot de passe').fill(PASSWORD);
  await page.getByRole('button', { name: 'Créer mon compte' }).click();
  await expect(page).toHaveURL(/\/client\/profil/);

  // Rechargement complet : le jeton (en mémoire) est perdu, la session revient via le cookie httpOnly
  await page.reload();
  await expect(page.getByRole('button', { name: 'Menu utilisateur' })).toBeVisible();

  // Un client n'accède ni à la file des analystes ni à l'administration
  await page.goto(`${BASE}/analyste/file`);
  await expect(page.getByRole('heading', { name: 'Accès refusé' })).toBeVisible();
  await page.goto(`${BASE}/admin/utilisateurs`);
  await expect(page.getByRole('heading', { name: 'Accès refusé' })).toBeVisible();

  // Déconnexion : le cookie est révoqué, un rechargement ne restaure plus la session
  await page.getByRole('button', { name: 'Menu utilisateur' }).click();
  await page.getByRole('menuitem', { name: 'Se déconnecter' }).click();
  await expect(page).toHaveURL(/\/connexion/);
  await page.goto(`${BASE}/client/demandes`);
  await expect(page).toHaveURL(/\/connexion/);
});

test('administration : création d\'un analyste puis désactivation du compte', async ({ browser }) => {
  const adminCtx = await browser.newContext({ locale: 'fr-FR' });
  const admin = await adminCtx.newPage();
  await login(admin, ADMIN, DEMO_PASSWORD);
  await admin.goto(`${BASE}/admin/utilisateurs`);

  const email = `analyste.${unique()}@e2e.test`;
  const tempPassword = 'Analyste-E2e-2026!';
  await admin.getByRole('button', { name: 'Créer un analyste' }).click();
  const dialog = admin.getByRole('dialog');
  await dialog.getByLabel('Prénom').fill('Omar');
  await dialog.getByLabel('Nom', { exact: true }).fill('Nouveau');
  await dialog.getByLabel('Adresse e-mail').fill(email);
  await dialog.getByLabel('Mot de passe temporaire').fill(tempPassword);
  await dialog.getByTestId('create-user-submit').click();
  await expect(dialog).toBeHidden();

  // Le nouvel analyste se connecte
  const analystCtx = await browser.newContext({ locale: 'fr-FR' });
  const analyst = await analystCtx.newPage();
  await login(analyst, email, tempPassword);
  await expect(analyst).toHaveURL(/\/analyste\/file/);

  // L'admin désactive le compte : la connexion est refusée et la session existante ne se restaure plus
  await admin.getByLabel('Recherche').fill(email);
  const row = admin.getByRole('row', { name: new RegExp(email.replace(/\./g, '\\.')) });
  await expect(row).toBeVisible();
  await row.getByRole('switch').click();
  await admin.getByRole('dialog').getByRole('button', { name: 'Désactiver' }).click();
  await expect(row.getByText('Désactivé')).toBeVisible();

  await analyst.reload();
  await expect(analyst).toHaveURL(/\/connexion/);
  await analyst.getByLabel('Adresse e-mail').fill(email);
  await analyst.getByLabel('Mot de passe', { exact: true }).fill(tempPassword);
  await analyst.getByRole('button', { name: 'Se connecter' }).click();
  await expect(analyst.getByRole('alert')).toBeVisible();

  await adminCtx.close();
  await analystCtx.close();
});
