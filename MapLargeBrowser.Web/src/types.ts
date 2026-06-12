export type EntryType = 'File' | 'Directory' | 'Symlink';

export interface FileEntry {
  name: string;
  relativePath: string;
  type: EntryType;
  size: number;
  modifiedUtc: string;
  symlinkTarget: string | null;
}

export interface BrowseResponse {
  path: string;
  entries: FileEntry[];
  fileCount: number;
  directoryCount: number;
  immediateSize: number;
  rootIsResettable: boolean;
}

export interface SearchResponse {
  entries: FileEntry[];
  fileCount: number;
  directoryCount: number;
  totalSize: number;
  truncated: boolean;
}

export interface EntryReference {
  from: string;
  to: string;
}
