import { PDF_BYTES, PNG_BYTES, makeFile } from '../../../testing/test-helpers';
import {
  MAX_FILE_SIZE_BYTES,
  formatFileSize,
  hasAcceptedSignature,
  validateFile,
  validateFileDeep,
} from './file-validation';

describe('file-validation', () => {
  it('accepte un PDF de taille raisonnable', () => {
    expect(validateFile(makeFile('cin.pdf', 'application/pdf', PDF_BYTES))).toBeNull();
  });

  it('refuse un fichier de plus de 5 Mo', () => {
    expect(
      validateFile(makeFile('gros.pdf', 'application/pdf', PDF_BYTES, MAX_FILE_SIZE_BYTES + 1)),
    ).toBe('too-large');
  });

  it('refuse un fichier vide', () => {
    expect(validateFile(makeFile('vide.pdf', 'application/pdf', []))).toBe('empty');
  });

  it('refuse un type ou une extension non autorisés', () => {
    expect(validateFile(makeFile('macro.docx', 'application/msword', PDF_BYTES))).toBe('bad-type');
    expect(validateFile(makeFile('faux.exe', 'application/pdf', PDF_BYTES))).toBe('bad-type');
  });

  it('reconnaît les signatures PDF, JPEG et PNG', () => {
    expect(hasAcceptedSignature(new Uint8Array(PDF_BYTES))).toBe(true);
    expect(hasAcceptedSignature(new Uint8Array(PNG_BYTES))).toBe(true);
    expect(hasAcceptedSignature(new Uint8Array([0xff, 0xd8, 0xff, 0xe0]))).toBe(true);
    expect(hasAcceptedSignature(new Uint8Array([0x4d, 0x5a, 0x90, 0x00]))).toBe(false);
  });

  it('détecte un fichier dont le contenu ne correspond pas à l’extension', async () => {
    expect(
      await validateFileDeep(
        makeFile('script.pdf', 'application/pdf', [0x4d, 0x5a, 0x90, 0x00, 1, 2, 3, 4]),
      ),
    ).toBe('bad-signature');
    expect(await validateFileDeep(makeFile('ok.png', 'image/png', PNG_BYTES))).toBeNull();
  });

  it('formate les tailles en français', () => {
    expect(formatFileSize(512)).toBe('512 o');
    expect(formatFileSize(2048)).toBe('2 Ko');
    expect(formatFileSize(1.5 * 1024 * 1024)).toBe('1,5 Mo');
  });
});
