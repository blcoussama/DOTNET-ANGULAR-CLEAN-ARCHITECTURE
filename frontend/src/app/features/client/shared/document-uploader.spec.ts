import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { doc } from '../../../../testing/fixtures';
import {
  PDF_BYTES,
  PNG_BYTES,
  makeFile,
  provideTestEnvironment,
  settle,
  signIn,
  testId,
  text,
} from '../../../../testing/test-helpers';
import { ApplicationDocument } from '../../../core/models/api.models';
import { MAX_FILE_SIZE_BYTES } from '../../../core/util/file-validation';
import { DocumentUploader, missingRequiredDocuments } from './document-uploader';

describe('DocumentUploader', () => {
  let fixture: ComponentFixture<DocumentUploader>;
  let http: HttpTestingController;
  let uploaded: ApplicationDocument[];

  beforeEach(async () => {
    TestBed.configureTestingModule({
      imports: [DocumentUploader],
      providers: provideTestEnvironment(),
    });
    http = TestBed.inject(HttpTestingController);
    signIn('Client');
    fixture = TestBed.createComponent(DocumentUploader);
    fixture.componentRef.setInput('applicationId', 'app-1');
    fixture.componentRef.setInput('documents', []);
    uploaded = [];
    fixture.componentInstance.uploaded.subscribe((d) => uploaded.push(d));
    await settle(fixture);
  });

  afterEach(() => http.verify());

  it('signale les pièces obligatoires manquantes', () => {
    expect(testId(fixture, 'missing-docs')?.textContent).toContain("Carte d'identité");
    expect(testId(fixture, 'missing-docs')?.textContent).toContain('Bulletin de salaire');
    expect(missingRequiredDocuments([doc('1', 'ID_CARD')])).toEqual(['PAYSLIP']);
    expect(missingRequiredDocuments([doc('1', 'ID_CARD'), doc('2', 'PAYSLIP')])).toEqual([]);
  });

  it('refuse un fichier de plus de 5 Mo sans appeler l’API', async () => {
    await fixture.componentInstance.handleFiles('ID_CARD', [
      makeFile('cin.pdf', 'application/pdf', PDF_BYTES, MAX_FILE_SIZE_BYTES + 10),
    ]);
    await settle(fixture);
    expect(testId(fixture, 'error-ID_CARD')?.textContent).toContain('5 Mo');
    http.expectNone('/api/v1/applications/app-1/documents');
  });

  it('refuse un type non accepté et un contenu falsifié', async () => {
    await fixture.componentInstance.handleFiles('PAYSLIP', [
      makeFile('paie.gif', 'image/gif', [0x47, 0x49, 0x46]),
    ]);
    await settle(fixture);
    expect(testId(fixture, 'error-PAYSLIP')?.textContent).toContain('PDF, JPEG et PNG');
    await fixture.componentInstance.handleFiles('PAYSLIP', [
      makeFile('paie.pdf', 'application/pdf', [0x4d, 0x5a, 0, 0, 0, 0, 0, 0]),
    ]);
    await settle(fixture);
    expect(testId(fixture, 'error-PAYSLIP')?.textContent).toContain('ne correspond pas');
    http.expectNone('/api/v1/applications/app-1/documents');
  });

  it('envoie un fichier valide, affiche la progression et émet la pièce déposée', async () => {
    const done = fixture.componentInstance.handleFiles('ID_CARD', [
      makeFile('cin.png', 'image/png', PNG_BYTES),
    ]);
    await settle(fixture);
    const req = http.expectOne('/api/v1/applications/app-1/documents');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-Client');
    req.event({ type: 1, loaded: 30, total: 100 });
    await settle(fixture);
    expect(text(fixture)).toContain('Envoi de cin.png… 30 %');
    req.flush(doc('d9', 'ID_CARD'));
    await done;
    expect(uploaded.map((d) => d.id)).toEqual(['d9']);
  });

  it('accepte un fichier choisi via le sélecteur', async () => {
    const input = testId<HTMLInputElement>(fixture, 'file-input-PAYSLIP')!;
    Object.defineProperty(input, 'files', {
      value: [makeFile('paie.pdf', 'application/pdf', PDF_BYTES)],
    });
    input.dispatchEvent(new Event('change'));
    await settle(fixture, 4);
    http.expectOne('/api/v1/applications/app-1/documents').flush(doc('d3', 'PAYSLIP'));
    await settle(fixture);
    expect(uploaded.map((d) => d.type)).toEqual(['PAYSLIP']);
  });

  it('affiche l’erreur serveur (ex. signature refusée)', async () => {
    const done = fixture.componentInstance.handleFiles('OTHER', [
      makeFile('a.pdf', 'application/pdf', PDF_BYTES),
    ]);
    await settle(fixture);
    http
      .expectOne('/api/v1/applications/app-1/documents')
      .flush(
        { title: 'Unsupported', detail: 'Signature de fichier invalide.' },
        { status: 415, statusText: 'Unsupported Media Type' },
      );
    await done;
    await settle(fixture);
    expect(testId(fixture, 'error-OTHER')?.textContent).toContain('Signature de fichier invalide.');
    expect(uploaded).toEqual([]);
  });
});
