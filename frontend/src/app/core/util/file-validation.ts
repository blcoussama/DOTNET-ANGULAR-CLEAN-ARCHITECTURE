/** Contraintes de dépôt alignées sur l'API (SPEC §2 : PDF / JPEG / PNG, 5 Mo max). */
export const MAX_FILE_SIZE_BYTES = 5 * 1024 * 1024;
export const ACCEPTED_MIME_TYPES = ['application/pdf', 'image/jpeg', 'image/png'] as const;
export const ACCEPTED_EXTENSIONS = ['.pdf', '.jpg', '.jpeg', '.png'] as const;
export const ACCEPT_ATTRIBUTE = [...ACCEPTED_MIME_TYPES, ...ACCEPTED_EXTENSIONS].join(',');

export type FileValidationError = 'empty' | 'too-large' | 'bad-type' | 'bad-signature';

export const FILE_ERROR_MESSAGES: Record<FileValidationError, string> = {
  empty: 'Le fichier est vide.',
  'too-large': 'Le fichier dépasse la taille maximale de 5 Mo.',
  'bad-type': 'Format non accepté : seuls les fichiers PDF, JPEG et PNG sont autorisés.',
  'bad-signature': 'Le contenu du fichier ne correspond pas à un PDF, JPEG ou PNG valide.',
};

function extensionOf(name: string): string {
  const index = name.lastIndexOf('.');
  return index >= 0 ? name.slice(index).toLowerCase() : '';
}

/** Contrôle synchrone : taille, type MIME déclaré et extension. */
export function validateFile(file: File): FileValidationError | null {
  if (file.size === 0) return 'empty';
  if (file.size > MAX_FILE_SIZE_BYTES) return 'too-large';
  const mimeOk = (ACCEPTED_MIME_TYPES as readonly string[]).includes(file.type);
  const extOk = (ACCEPTED_EXTENSIONS as readonly string[]).includes(extensionOf(file.name));
  if (!mimeOk || !extOk) return 'bad-type';
  return null;
}

/** Vérifie la signature binaire (magic bytes), comme le serveur, qui reste l'autorité. */
export function hasAcceptedSignature(bytes: Uint8Array): boolean {
  const startsWith = (sig: number[]) => sig.every((b, i) => bytes[i] === b);
  return (
    startsWith([0x25, 0x50, 0x44, 0x46, 0x2d]) || // %PDF-
    startsWith([0xff, 0xd8, 0xff]) || // JPEG
    startsWith([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]) // PNG
  );
}

async function readHead(file: Blob, length: number): Promise<Uint8Array> {
  const slice = file.slice(0, length);
  if (typeof slice.arrayBuffer === 'function') {
    return new Uint8Array(await slice.arrayBuffer());
  }
  return new Promise<Uint8Array>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(new Uint8Array(reader.result as ArrayBuffer));
    reader.onerror = () => reject(reader.error);
    reader.readAsArrayBuffer(slice);
  });
}

/** Validation complète (synchrone + signature). */
export async function validateFileDeep(file: File): Promise<FileValidationError | null> {
  const basic = validateFile(file);
  if (basic) return basic;
  try {
    const head = await readHead(file, 8);
    return hasAcceptedSignature(head) ? null : 'bad-signature';
  } catch {
    // Lecture impossible : on laisse le serveur trancher.
    return null;
  }
}

export function formatFileSize(bytes: number): string {
  if (!Number.isFinite(bytes) || bytes < 0) return '';
  if (bytes < 1024) return `${bytes} o`;
  if (bytes < 1024 * 1024)
    return `${(bytes / 1024).toLocaleString('fr-FR', { maximumFractionDigits: 0 })} Ko`;
  return `${(bytes / (1024 * 1024)).toLocaleString('fr-FR', { maximumFractionDigits: 1 })} Mo`;
}
