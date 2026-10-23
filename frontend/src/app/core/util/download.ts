/**
 * Déclenche l'enregistrement d'un Blob obtenu via HttpClient (le jeton est envoyé par l'intercepteur,
 * ce qu'un simple lien `<a href>` ne permettrait pas).
 */
export function saveBlob(blob: Blob, fileName: string, doc: Document = document): void {
  const url = URL.createObjectURL(blob);
  const anchor = doc.createElement('a');
  anchor.href = url;
  anchor.download = sanitizeFileName(fileName);
  anchor.rel = 'noopener';
  anchor.style.display = 'none';
  doc.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

/** Retire les séparateurs de chemin et caractères de contrôle d'un nom de fichier fourni par le serveur. */
export function sanitizeFileName(name: string): string {
  // eslint-disable-next-line no-control-regex
  const cleaned = (name ?? '').replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').trim();
  return cleaned || 'document';
}
