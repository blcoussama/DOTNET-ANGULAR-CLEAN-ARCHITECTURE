import { Pipe, PipeTransform } from '@angular/core';
import { formatFileSize } from '../../core/util/file-validation';

@Pipe({ name: 'fileSize' })
export class FileSizePipe implements PipeTransform {
  transform(bytes: number | null | undefined): string {
    return bytes === null || bytes === undefined ? '' : formatFileSize(bytes);
  }
}
