import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { application, doc } from '../../../../testing/fixtures';
import {
  click,
  provideTestEnvironment,
  settle,
  signIn,
  testId,
  text,
} from '../../../../testing/test-helpers';
import { ApplicationDetail } from '../../../core/models/api.models';
import { ApplicationDetailPage } from './application-detail-page';

describe('ApplicationDetailPage — détail client', () => {
  let fixture: ComponentFixture<ApplicationDetailPage>;
  let http: HttpTestingController;
  const url = '/api/v1/applications/app-1';

  async function create(detail: ApplicationDetail): Promise<void> {
    fixture = TestBed.createComponent(ApplicationDetailPage);
    fixture.componentRef.setInput('id', 'app-1');
    await settle(fixture);
    http.expectOne(url).flush(detail);
    await settle(fixture);
  }

  function confirmOverlay(label: string): void {
    click(
      [...document.querySelectorAll('.cdk-overlay-container button')].find((b) =>
        b.textContent?.includes(label),
      ) ?? null,
    );
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [ApplicationDetailPage],
      providers: provideTestEnvironment(),
    });
    http = TestBed.inject(HttpTestingController);
    signIn('Client');
  });

  afterEach(() => {
    http.verify();
    document.querySelector('.cdk-overlay-container')?.replaceChildren();
  });

  it('affiche les chiffres figés et l’historique d’une demande soumise', async () => {
    await create(
      application({
        status: 'SUBMITTED',
        annualRate: 7.5,
        monthlyPayment: 1934.4,
        totalCost: 12851.2,
        debtRatio: 0.262,
        submittedAt: '2026-10-02T09:00:00Z',
        events: [
          {
            id: 'e1',
            applicationId: 'app-1',
            fromStatus: 'DRAFT',
            toStatus: 'SUBMITTED',
            actorId: 'u',
            actorRole: 'Client',
            occurredAt: '2026-10-02T09:00:00Z',
          },
        ],
      }),
    );
    expect(testId(fixture, 'frozen-terms')?.textContent).toContain('7,5');
    expect(text(fixture)).toContain('Brouillon → Soumise');
    // Le score n'est pas montré au client.
    expect(text(fixture)).not.toContain('Score');
  });

  it('présente la demande de compléments et permet de renvoyer le dossier', async () => {
    await create(
      application({
        status: 'INFO_REQUESTED',
        documents: [doc('d1', 'ID_CARD'), doc('d2', 'PAYSLIP')],
        events: [
          {
            id: 'e3',
            applicationId: 'app-1',
            fromStatus: 'UNDER_REVIEW',
            toStatus: 'INFO_REQUESTED',
            actorId: 'a',
            actorRole: 'Analyst',
            comment: 'Merci de joindre un relevé bancaire.',
            occurredAt: '2026-10-04T09:00:00Z',
          },
        ],
      }),
    );
    expect(testId(fixture, 'info-request')?.textContent).toContain(
      'Merci de joindre un relevé bancaire.',
    );
    click(testId(fixture, 'resubmit'));
    await settle(fixture);
    confirmOverlay('Renvoyer');
    await settle(fixture);
    http.expectOne(`${url}/submit`).flush(application({ status: 'SUBMITTED' }));
    await settle(fixture);
    http.expectOne(url).flush(application({ status: 'SUBMITTED' }));
  });

  it('annule la demande après confirmation', async () => {
    await create(application({ status: 'SUBMITTED' }));
    click(testId(fixture, 'cancel-application'));
    await settle(fixture);
    confirmOverlay('Annuler la demande');
    await settle(fixture);
    http.expectOne(`${url}/cancel`).flush(application({ status: 'CANCELLED' }));
    await settle(fixture);
    http.expectOne(url).flush(application({ status: 'CANCELLED' }));
    await settle(fixture);
    expect(testId(fixture, 'cancel-application')).toBeNull();
  });

  it('n’envoie rien si l’annulation est abandonnée', async () => {
    await create(application({ status: 'DRAFT' }));
    click(testId(fixture, 'cancel-application'));
    await settle(fixture);
    confirmOverlay('Conserver');
    await settle(fixture);
    http.expectNone(`${url}/cancel`);
  });

  it('affiche « demande introuvable » pour une demande d’un autre client (404)', async () => {
    fixture = TestBed.createComponent(ApplicationDetailPage);
    fixture.componentRef.setInput('id', 'app-1');
    await settle(fixture);
    http.expectOne(url).flush({ title: 'Not Found' }, { status: 404, statusText: 'Not Found' });
    await settle(fixture);
    expect(text(fixture)).toContain('Demande introuvable');
  });
});
