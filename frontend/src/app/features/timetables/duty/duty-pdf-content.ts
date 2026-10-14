// Validate the retained artifact, allowing valid MIME parameters used by proxies.
// A successful JSON/HTML response must never be presented as an official PDF.
export async function isDutyPdf(blob: Blob | null): Promise<boolean> {
  if (!blob || blob.type.split(';', 1)[0].trim().toLowerCase() !== 'application/pdf' || blob.size < 100) return false;
  return (await blob.slice(0, 5).text()) === '%PDF-';
}
