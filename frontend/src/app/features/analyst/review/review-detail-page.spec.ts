import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { reviewDetail } from '../../../../testing/fixtures';
import {
  click,
  overlay,
  provideTestEnvironment,
  settle,
  signIn,
  testId,
  text,
  typeInto,
} from '../../../../testing/test-helpers';
import { ReviewApplicationDetailRaw, Role } from '../../../core/models/api.models';
import { ReviewDetailPage } from './review-detail-page';

describe('ReviewDetailPage — dossier analyste', () => {
  let fixture: ComponentFixture<ReviewDetailPage>;
  let http: HttpTestingController;
  const url = '/api/v1/review/applications/app-1';

  async function create(
    detail: ReviewApplicationDetailRaw = reviewDetail(),
    role: Role = 'Analyst',
  ): Promise<void> {
    signIn(role);
    fixture = TestBed.createComponent(ReviewDetailPage);
    fixture.componentRef.setInput('id', 'app-1');
    await settle(fixture);
    http.expectOne(url).flush(detail);
    await settle(fixture);
  }

  function confirmOverlayButton(label: string): void {
    const btn = [...document.querySelectorAll('.cdk-overlay-container button')].find((b) =>
      b.textContent?.includes(label),
    );
    click(btn ?? null);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ReviewDetailPage],
      providers: provideTestEnvironment(),
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
    document.querySelector('.cdk-overlay-container')?.replaceChildren();
  });

  it('affiche le score, le grade, la recommandation et la décomposition par critère', async () => {
    await create();
    expect(testId(fixture, 'score-value')?.textContent).toBe('82');
    expect(text(fixture)).toContain('Recommandation : Favorable');
    const criteria = fixture.nativeElement.querySelectorAll('[data-testid="criterion"]');
    expect(criteria.length).toBe(4);
    expect(criteria[0].textContent).toContain("Taux d'endettement");
    expect(criteria[0].textContent).toContain('40 / 40 pts');
    const meter = criteria[3].querySelector('[role="meter"]') as HTMLElement;
    expect(meter.getAttribute('aria-valuenow')).toBe('5');
    expect(meter.getAttribute('aria-valuemax')).toBe('20');
  });

  it('affiche le profil client, l’endettement et les pièces téléchargeables', async () => {
    await create();
    expect(testId(fixture, 'client-profile')?.textContent).toContain('Salma Bennani');
    expect(testId(fixture, 'client-profile')?.textContent).toContain('CDI');
    expect(testId(fixture, 'review-debt')?.textContent).toContain('26');
    expect(fixture.nativeElement.querySelectorAll('button[aria-label^="Télécharger"]').length).toBe(
      2,
    );
  });

  it('télécharge une pièce en Blob avec le jeton', async () => {
    const createObjectURL = vi.fn(() => 'blob:x');
    const revokeObjectURL = vi.fn();
    Object.assign(URL, { createObjectURL, revokeObjectURL });
    const anchorClick = vi
      .spyOn(HTMLAnchorElement.prototype, 'click')
      .mockImplementation(() => undefined);
    await create();
    click(fixture.nativeElement.querySelector('button[aria-label="Télécharger id_card.pdf"]'));
    const req = http.expectOne('/api/v1/applications/app-1/documents/d1');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-Analyst');
    req.flush(new Blob(['%PDF-']));
    expect(createObjectURL).toHaveBeenCalled();
    expect(anchorClick).toHaveBeenCalled();
    anchorClick.mockRestore();
  });

  it('permet de s’assigner un dossier soumis, après confirmation', async () => {
    await create(reviewDetail({ status: 'SUBMITTED', assignedAnalystId: null }));
    expect(testId(fixture, 'approve')).toBeNull();
    click(testId(fixture, 'assign'));
    await settle(fixture);
    confirmOverlayButton('M’assigner le dossier');
    await settle(fixture);
    const req = http.expectOne(`${url}/assign`);
    expect(req.request.method).toBe('POST');
    req.flush({});
    await settle(fixture);
    http.expectOne(url).flush(reviewDetail());
  });

  it('approuve avec un commentaire obligatoire saisi dans la boîte de confirmation', async () => {
    await create();
    click(testId(fixture, 'approve'));
    await settle(fixture);
    const confirm = overlay<HTMLButtonElement>('[data-testid="decision-confirm"]');
    click(confirm);
    await settle(fixture);
    http.expectNone(`${url}/approve`);
    expect(document.querySelector('.cdk-overlay-container')?.textContent).toContain(
      'Le commentaire est obligatoire.',
    );

    typeInto(
      overlay<HTMLTextAreaElement>('[data-testid="decision-comment"]'),
      '  Capacité de remboursement confirmée.  ',
    );
    click(confirm);
    await settle(fixture);
    const req = http.expectOne(`${url}/approve`);
    expect(req.request.body).toEqual({ comment: 'Capacité de remboursement confirmée.' });
    req.flush({});
    await settle(fixture);
    http.expectOne(url).flush(
      reviewDetail({
        status: 'APPROVED',
        decisionComment: 'Capacité de remboursement confirmée.',
      }),
    );
    await settle(fixture);
    expect(text(fixture)).toContain('Approuvée');
  });

  it('demande des compléments', async () => {
    await create();
    click(testId(fixture, 'request-info'));
    await settle(fixture);
    typeInto(
      overlay<HTMLTextAreaElement>('[data-testid="decision-comment"]'),
      'Merci de joindre un relevé bancaire.',
    );
    click(overlay('[data-testid="decision-confirm"]'));
    await settle(fixture);
    http.expectOne(`${url}/request-info`).flush({});
    await settle(fixture);
    http.expectOne(url).flush(reviewDetail({ status: 'INFO_REQUESTED' }));
  });

  it('n’offre pas de décision à un analyste non assigné', async () => {
    await create(
      reviewDetail({ assignedAnalystId: 'someone-else', assignedAnalystName: 'K. Amrani' }),
    );
    expect(testId(fixture, 'approve')).toBeNull();
    expect(testId(fixture, 'reject')).toBeNull();
    expect(testId(fixture, 'not-assigned-hint')).not.toBeNull();
    expect(text(fixture)).toContain('Assigné à K. Amrani');
  });

  it('laisse l’administrateur décider même s’il n’est pas assigné', async () => {
    await create(reviewDetail({ assignedAnalystId: 'someone-else' }), 'Admin');
    expect(testId(fixture, 'reject')).not.toBeNull();
  });

  it('recharge le dossier en cas de conflit (409)', async () => {
    await create();
    click(testId(fixture, 'reject'));
    await settle(fixture);
    typeInto(
      overlay<HTMLTextAreaElement>('[data-testid="decision-comment"]'),
      'Endettement trop élevé.',
    );
    click(overlay('[data-testid="decision-confirm"]'));
    await settle(fixture);
    http
      .expectOne(`${url}/reject`)
      .flush(
        { title: 'Conflict', detail: 'Le dossier a été modifié par un autre utilisateur.' },
        { status: 409, statusText: 'Conflict' },
      );
    await settle(fixture);
    expect(document.querySelector('.cdk-overlay-container')?.textContent).toContain(
      'modifié par un autre utilisateur',
    );
    http.expectOne(url).flush(reviewDetail({ status: 'REJECTED' }));
  });

  it('affiche « dossier introuvable » sur 404', async () => {
    signIn('Analyst');
    fixture = TestBed.createComponent(ReviewDetailPage);
    fixture.componentRef.setInput('id', 'app-1');
    await settle(fixture);
    http.expectOne(url).flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);
    expect(text(fixture)).toContain('Dossier introuvable');
  });
});
