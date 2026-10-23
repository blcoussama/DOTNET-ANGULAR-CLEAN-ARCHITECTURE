import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { COMPLETE_PROFILE, PRODUCTS, doc } from '../../../testing/fixtures';
import { PDF_BYTES, makeFile, provideTestEnvironment } from '../../../testing/test-helpers';
import { AdminUsersApi } from './admin-users.api';
import { ApplicationsApi, UploadProgress } from './applications.api';
import { AuthApi } from './auth.api';
import { DashboardApi } from './dashboard.api';
import { ProductsApi } from './products.api';
import { ProfileApi } from './profile.api';
import { ReviewApi } from './review.api';
import { SimulationApi } from './simulation.api';

describe('Services API (contrat /api/v1)', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: provideTestEnvironment() });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('AuthApi', () => {
    it('envoie login et refresh avec les cookies (withCredentials)', () => {
      const api = TestBed.inject(AuthApi);
      api.login({ email: 'a@b.ma', password: 'p' }).subscribe();
      const login = http.expectOne('/api/v1/auth/login');
      expect(login.request.method).toBe('POST');
      expect(login.request.withCredentials).toBe(true);
      expect(login.request.body).toEqual({ email: 'a@b.ma', password: 'p' });
      login.flush({});
      api.refresh().subscribe();
      const refresh = http.expectOne('/api/v1/auth/refresh');
      expect(refresh.request.withCredentials).toBe(true);
      refresh.flush({});
    });
  });

  describe('ProfileApi', () => {
    it('renvoie un profil vide sur 404 et normalise la date', () => {
      const api = TestBed.inject(ProfileApi);
      let isComplete: boolean | undefined = true;
      api.getMine().subscribe((p) => (isComplete = p.isComplete));
      http
        .expectOne('/api/v1/clients/me/profile')
        .flush(null, { status: 404, statusText: 'Not Found' });
      expect(isComplete).toBe(false);

      let dob: string | null = null;
      api.getMine().subscribe((p) => (dob = p.dateOfBirth));
      http
        .expectOne('/api/v1/clients/me/profile')
        .flush({ ...COMPLETE_PROFILE, dateOfBirth: '1990-04-12T00:00:00Z' });
      expect(dob).toBe('1990-04-12');
    });

    it('met à jour le profil en PUT', () => {
      TestBed.inject(ProfileApi)
        .updateMine({ ...COMPLETE_PROFILE, employer: null } as never)
        .subscribe();
      const req = http.expectOne('/api/v1/clients/me/profile');
      expect(req.request.method).toBe('PUT');
      req.flush(COMPLETE_PROFILE);
    });
  });

  describe('ProductsApi', () => {
    it('filtre les produits inactifs et les trie dans l’ordre métier', () => {
      let types: string[] = [];
      TestBed.inject(ProductsApi)
        .listActive()
        .subscribe((p) => (types = p.map((x) => x.type)));
      http
        .expectOne('/api/v1/products')
        .flush([PRODUCTS[2], { ...PRODUCTS[1], isActive: false }, PRODUCTS[0]]);
      expect(types).toEqual(['CONSOMMATION', 'IMMOBILIER']);
    });

    it('met à jour un produit par son type (admin)', () => {
      const { type: _type, ...body } = PRODUCTS[1];
      TestBed.inject(ProductsApi).update('AUTO', body).subscribe();
      const req = http.expectOne('/api/v1/admin/products/AUTO');
      expect(req.request.method).toBe('PUT');
      expect(req.request.body).toEqual(body);
      req.flush(PRODUCTS[1]);
    });
  });

  describe('SimulationApi', () => {
    it('n’envoie revenu et charges que si le revenu est renseigné', () => {
      const api = TestBed.inject(SimulationApi);
      api
        .simulate({ productType: 'AUTO', amount: 100000, durationMonths: 48, monthlyIncome: 0 })
        .subscribe();
      expect(http.expectOne('/api/v1/simulations').request.body).toEqual({
        productType: 'AUTO',
        amount: 100000,
        durationMonths: 48,
      });
      api
        .simulate({ productType: 'AUTO', amount: 100000, durationMonths: 48, monthlyIncome: 12000 })
        .subscribe();
      expect(http.expectOne('/api/v1/simulations').request.body).toEqual({
        productType: 'AUTO',
        amount: 100000,
        durationMonths: 48,
        monthlyIncome: 12000,
        monthlyCharges: 0,
      });
    });
  });

  describe('ApplicationsApi', () => {
    it('liste mes demandes avec pagination et filtre de statut', () => {
      TestBed.inject(ApplicationsApi)
        .listMine({ page: 2, pageSize: 10, status: 'SUBMITTED' })
        .subscribe();
      const req = http.expectOne((r) => r.url === '/api/v1/applications/mine');
      expect(req.request.params.get('page')).toBe('2');
      expect(req.request.params.get('pageSize')).toBe('10');
      expect(req.request.params.get('status')).toBe('SUBMITTED');
      req.flush({ items: [], page: 2, pageSize: 10, totalCount: 0, totalPages: 0 });
    });

    it('appelle submit / cancel sur la bonne ressource (identifiant encodé)', () => {
      const api = TestBed.inject(ApplicationsApi);
      api.submit('a/b').subscribe();
      expect(http.expectOne('/api/v1/applications/a%2Fb/submit').request.method).toBe('POST');
      api.cancel('app-1').subscribe();
      expect(http.expectOne('/api/v1/applications/app-1/cancel').request.method).toBe('POST');
    });

    it('dépose une pièce en multipart (type + file) avec progression', () => {
      const events: UploadProgress[] = [];
      const file = makeFile('cin.pdf', 'application/pdf', PDF_BYTES);
      TestBed.inject(ApplicationsApi)
        .uploadDocument('app-1', 'ID_CARD', file)
        .subscribe((e) => events.push(e));
      const req = http.expectOne('/api/v1/applications/app-1/documents');
      const body = req.request.body as FormData;
      expect(body.get('type')).toBe('ID_CARD');
      expect((body.get('file') as File).name).toBe('cin.pdf');
      req.event({ type: 1, loaded: 50, total: 100 });
      req.flush(doc('d1', 'ID_CARD'));
      expect(events[0]).toEqual({ kind: 'progress', percent: 50 });
      expect(events.at(-1)).toEqual({ kind: 'done', document: doc('d1', 'ID_CARD') });
    });

    it('télécharge une pièce en Blob', () => {
      let size = 0;
      TestBed.inject(ApplicationsApi)
        .downloadDocument('app-1', 'd1')
        .subscribe((b) => (size = b.size));
      const req = http.expectOne('/api/v1/applications/app-1/documents/d1');
      expect(req.request.responseType).toBe('blob');
      req.flush(new Blob(['%PDF-']));
      expect(size).toBe(5);
    });

    it('complète documents/événements absents du détail', () => {
      let docs: unknown[] | undefined;
      TestBed.inject(ApplicationsApi)
        .get('app-1')
        .subscribe((d) => (docs = d.documents));
      http.expectOne('/api/v1/applications/app-1').flush({ id: 'app-1' });
      expect(docs).toEqual([]);
    });
  });

  describe('ReviewApi', () => {
    it('transmet filtres et tri de la file', () => {
      TestBed.inject(ReviewApi)
        .list({
          page: 1,
          pageSize: 20,
          status: 'SUBMITTED',
          riskGrade: 'B',
          productType: 'AUTO',
          assignedToMe: false,
          search: 'CF-2026',
          sortBy: 'riskScore',
        })
        .subscribe();
      const req = http.expectOne((r) => r.url === '/api/v1/review/applications');
      const p = req.request.params;
      expect(p.get('status')).toBe('SUBMITTED');
      expect(p.get('riskGrade')).toBe('B');
      expect(p.get('productType')).toBe('AUTO');
      expect(p.has('assignedToMe')).toBe(false);
      expect(p.get('search')).toBe('CF-2026');
      expect(p.get('sortBy')).toBe('riskScore');
      expect(p.get('sortDir')).toBe('desc');
      req.flush({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });
    });

    it('envoie la décision avec un commentaire nettoyé', () => {
      TestBed.inject(ReviewApi)
        .decide('app-1', 'request-info', '  Merci de fournir un relevé.  ')
        .subscribe();
      const req = http.expectOne('/api/v1/review/applications/app-1/request-info');
      expect(req.request.body).toEqual({ comment: 'Merci de fournir un relevé.' });
      req.flush({});
    });

    it('assigne le dossier', () => {
      TestBed.inject(ReviewApi).assign('app-1').subscribe();
      expect(http.expectOne('/api/v1/review/applications/app-1/assign').request.method).toBe(
        'POST',
      );
    });
  });

  describe('DashboardApi', () => {
    it('normalise les statistiques', () => {
      let total = 0;
      TestBed.inject(DashboardApi)
        .stats()
        .subscribe((s) => (total = s.totalApplications));
      http.expectOne('/api/v1/dashboard/stats').flush({
        countsByStatus: [
          { status: 'APPROVED', count: 2 },
          { status: 'REJECTED', count: 1 },
        ],
        approvalRate: 0.66,
        averageAmount: 1,
        totalApprovedAmount: 1,
        volumeByProduct: [],
        averageProcessingDays: 1,
        monthlyTrend: [],
        gradeDistribution: {},
      });
      expect(total).toBe(3);
    });
  });

  describe('AdminUsersApi', () => {
    it('liste, crée et modifie les utilisateurs', () => {
      const api = TestBed.inject(AdminUsersApi);
      api.list({ page: 1, pageSize: 20, role: 'Analyst', search: 'tazi' }).subscribe();
      const list = http.expectOne((r) => r.url === '/api/v1/admin/users');
      expect(list.request.params.get('role')).toBe('Analyst');
      list.flush({ items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 });

      api
        .create({
          email: 'n@b.ma',
          firstName: 'N',
          lastName: 'B',
          role: 'Analyst',
          temporaryPassword: 'Aa1!aaaa',
        })
        .subscribe();
      expect(http.expectOne('/api/v1/admin/users').request.method).toBe('POST');

      api.update('u-1', { isActive: false }).subscribe();
      const patch = http.expectOne('/api/v1/admin/users/u-1');
      expect(patch.request.method).toBe('PATCH');
      expect(patch.request.body).toEqual({ isActive: false });
    });
  });
});
