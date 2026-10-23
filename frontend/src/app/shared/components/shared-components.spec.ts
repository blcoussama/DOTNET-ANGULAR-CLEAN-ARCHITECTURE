import { TestBed } from '@angular/core/testing';
import { doc } from '../../../testing/fixtures';
import { provideTestEnvironment, text } from '../../../testing/test-helpers';
import { ApplicationEvent } from '../../core/models/api.models';
import { LabelPipe } from '../pipes/label.pipe';
import { MadPipe, formatMad } from '../pipes/mad.pipe';
import { DocumentList } from './document-list';
import { GradeBadge } from './grade-badge';
import { StatusBadge } from './status-badge';
import { Timeline } from './timeline';

describe('Composants partagés', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: provideTestEnvironment() }));

  it('affiche le libellé français et la classe de couleur du statut', async () => {
    const fixture = TestBed.createComponent(StatusBadge);
    fixture.componentRef.setInput('status', 'INFO_REQUESTED');
    await fixture.whenStable();
    const badge = fixture.nativeElement.querySelector('span') as HTMLElement;
    expect(badge.textContent).toContain('Compléments demandés');
    expect(badge.classList).toContain('cf-status-info-requested');
  });

  it('expose un libellé accessible pour le grade', async () => {
    const fixture = TestBed.createComponent(GradeBadge);
    fixture.componentRef.setInput('grade', 'B');
    fixture.componentRef.setInput('score', 71);
    await fixture.whenStable();
    const badge = fixture.nativeElement.querySelector('.cf-grade') as HTMLElement;
    expect(badge.getAttribute('aria-label')).toBe('Grade B — risque faible, score 71 sur 100');
    expect(badge.classList).toContain('cf-grade-b');
  });

  it('trie la timeline du plus récent au plus ancien et rend les commentaires en texte', async () => {
    const events: ApplicationEvent[] = [
      {
        id: '1',
        applicationId: 'a',
        toStatus: 'SUBMITTED',
        fromStatus: 'DRAFT',
        actorId: 'c',
        actorRole: 'Client',
        occurredAt: '2026-10-01T08:00:00Z',
      },
      {
        id: '2',
        applicationId: 'a',
        toStatus: 'INFO_REQUESTED',
        fromStatus: 'UNDER_REVIEW',
        actorId: 'x',
        actorRole: 'Analyst',
        comment: '<img src=x onerror=alert(1)>',
        occurredAt: '2026-10-03T08:00:00Z',
      },
    ];
    const fixture = TestBed.createComponent(Timeline);
    fixture.componentRef.setInput('events', events);
    await fixture.whenStable();
    const items = fixture.nativeElement.querySelectorAll('li');
    expect(items[0].textContent).toContain('Compléments demandés');
    expect(items[1].textContent).toContain('Brouillon → Soumise');
    // Aucun HTML serveur interprété.
    expect(fixture.nativeElement.querySelector('img')).toBeNull();
    expect(text(fixture)).toContain('<img src=x onerror=alert(1)>');
  });

  it('émet le téléchargement d’une pièce', async () => {
    const fixture = TestBed.createComponent(DocumentList);
    fixture.componentRef.setInput('documents', [doc('d1', 'PAYSLIP')]);
    const downloads: string[] = [];
    fixture.componentInstance.download.subscribe((d) => downloads.push(d.id));
    await fixture.whenStable();
    expect(text(fixture)).toContain('Bulletin de salaire');
    (
      fixture.nativeElement.querySelector('button[aria-label^="Télécharger"]') as HTMLButtonElement
    ).click();
    expect(downloads).toEqual(['d1']);
    expect(fixture.nativeElement.querySelector('button[aria-label^="Supprimer"]')).toBeNull();
  });

  it('formate les montants en MAD (fr-FR) et les libellés d’énumération', () => {
    expect(formatMad(12345.5, 2)).toMatch(/^12\s345,50\sMAD$/);
    expect(formatMad(null)).toBe('—');
    expect(new MadPipe().transform(5000)).toMatch(/^5\s000\sMAD$/);
    const label = new LabelPipe();
    expect(label.transform('IMMOBILIER', 'product')).toBe('Crédit immobilier');
    expect(label.transform('Analyst', 'role')).toBe('Analyste');
    expect(label.transform('INCONNU', 'status')).toBe('INCONNU');
    expect(label.transform(null, 'status')).toBe('—');
  });
});
