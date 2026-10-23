import { HttpTestingController, TestRequest } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { application } from '../../../../testing/fixtures';
import {
  click,
  provideTestEnvironment,
  settle,
  signIn,
  testId,
  text,
  typeInto,
} from '../../../../testing/test-helpers';
import { Page, ReviewApplicationSummary } from '../../../core/models/api.models';
import { ReviewQueuePage, queryFromParams, queryToParams } from './review-queue-page';

const isQueue = (r: { url: string }) => r.url === '/api/v1/review/applications';

function page(
  items: ReviewApplicationSummary[],
  totalCount = items.length,
): Page<ReviewApplicationSummary> {
  return { items, page: 1, pageSize: 20, totalCount, totalPages: Math.ceil(totalCount / 20) };
}

const ROWS: ReviewApplicationSummary[] = [
  {
    ...application({
      id: 'a1',
      reference: 'CF-2026-000001',
      status: 'SUBMITTED',
      riskScore: 82,
      riskGrade: 'A',
      submittedAt: '2026-10-01T08:00:00Z',
    }),
    clientName: 'Salma Bennani',
  },
  {
    ...application({
      id: 'a2',
      reference: 'CF-2026-000002',
      status: 'UNDER_REVIEW',
      riskScore: 41,
      riskGrade: 'D',
      assignedAnalystId: 'u-analyst',
    }),
    clientName: 'Omar Fassi',
  },
];

describe('ReviewQueuePage — file d’attente analyste', () => {
  let fixture: ComponentFixture<ReviewQueuePage>;
  let http: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;

  async function create(): Promise<TestRequest> {
    fixture = TestBed.createComponent(ReviewQueuePage);
    await settle(fixture);
    return http.expectOne(isQueue);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReviewQueuePage],
      providers: provideTestEnvironment(),
    });
    http = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    signIn('Analyst');
  });

  afterEach(() => http.verify());

  it('charge la première page triée par date de soumission (la plus ancienne d’abord)', async () => {
    const req = await create();
    const p = req.request.params;
    expect(p.get('page')).toBe('1');
    expect(p.get('pageSize')).toBe('20');
    expect(p.get('sortBy')).toBe('submittedAt');
    expect(p.get('sortDir')).toBe('asc');
    expect(p.has('status')).toBe(false);
    req.flush(page(ROWS));
    await settle(fixture);
    expect(fixture.nativeElement.querySelectorAll('tr.mat-mdc-row').length).toBe(2);
    expect(text(fixture)).toContain('Salma Bennani');
    expect(text(fixture)).toContain('Moi');
    expect(fixture.nativeElement.querySelector('a[href="/analyste/dossiers/a1"]')).not.toBeNull();
  });

  it('affiche l’état vide', async () => {
    (await create()).flush(page([]));
    await settle(fixture);
    expect(text(fixture)).toContain('Aucune demande à instruire');
  });

  it('affiche l’état d’erreur et permet de réessayer', async () => {
    (await create()).flush(null, { status: 500, statusText: 'Server Error' });
    await settle(fixture);
    expect(text(fixture)).toContain('Impossible de charger la file');
    click(
      [...fixture.nativeElement.querySelectorAll('button')].find((b: HTMLElement) =>
        b.textContent?.includes('Réessayer'),
      ),
    );
    await settle(fixture);
    http.expectOne(isQueue).flush(page(ROWS));
  });

  it('filtre par statut et grade en revenant à la page 1, et synchronise l’URL', async () => {
    (await create()).flush(page(ROWS, 45));
    await settle(fixture);
    fixture.componentInstance['onPage']({ pageIndex: 2, pageSize: 20, length: 45 });
    await settle(fixture);
    expect(http.expectOne(isQueue).request.params.get('page')).toBe('3');
    fixture.componentInstance['patch']({ status: 'SUBMITTED', riskGrade: 'A' });
    await settle(fixture);
    const req = http.expectOne(isQueue);
    expect(req.request.params.get('status')).toBe('SUBMITTED');
    expect(req.request.params.get('riskGrade')).toBe('A');
    expect(req.request.params.get('page')).toBe('1');
    req.flush(page(ROWS.slice(0, 1)));
    expect(navigate).toHaveBeenLastCalledWith(
      [],
      expect.objectContaining({
        queryParams: expect.objectContaining({ status: 'SUBMITTED', riskGrade: 'A' }),
        replaceUrl: true,
      }),
    );
  });

  it('filtre « assignées à moi » via l’interrupteur', async () => {
    (await create()).flush(page(ROWS));
    await settle(fixture);
    click(testId(fixture, 'filter-mine')!.querySelector('button'));
    await settle(fixture);
    expect(http.expectOne(isQueue).request.params.get('assignedToMe')).toBe('true');
  });

  it('trie côté serveur par montant ou score', async () => {
    (await create()).flush(page(ROWS));
    await settle(fixture);
    fixture.componentInstance['onSort']({ active: 'riskScore', direction: 'desc' });
    await settle(fixture);
    const req = http.expectOne(isQueue);
    expect(req.request.params.get('sortBy')).toBe('riskScore');
    expect(req.request.params.get('sortDir')).toBe('desc');
  });

  it('recherche par référence ou nom avec anti-rebond', async () => {
    (await create()).flush(page(ROWS));
    await settle(fixture);
    typeInto(testId<HTMLInputElement>(fixture, 'queue-search'), 'CF-2026-0000');
    typeInto(testId<HTMLInputElement>(fixture, 'queue-search'), 'CF-2026-000001');
    await settle(fixture);
    http.expectNone(isQueue);
    await new Promise((r) => setTimeout(r, 350));
    await settle(fixture);
    expect(http.expectOne(isQueue).request.params.get('search')).toBe('CF-2026-000001');
  });

  it('lit et écrit les filtres d’URL de façon sûre', () => {
    const q = queryFromParams({
      page: '2',
      pageSize: '999',
      status: 'DRAFT',
      riskGrade: 'C',
      sortBy: 'hack',
      assignedToMe: 'true',
    });
    expect(q).toMatchObject({
      page: 2,
      pageSize: 20,
      status: null,
      riskGrade: 'C',
      sortBy: 'submittedAt',
      assignedToMe: true,
    });
    expect(queryToParams(q)).toEqual({
      page: '2',
      pageSize: null,
      status: null,
      riskGrade: 'C',
      productType: null,
      assignedToMe: 'true',
      search: null,
      sortBy: null,
      sortDir: null,
    });
  });
});
