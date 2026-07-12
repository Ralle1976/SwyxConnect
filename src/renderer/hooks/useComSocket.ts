import { useCallback, useEffect, useRef, useState } from 'react';
import type {
  PhoneBookEntry,
  PhoneBookReply,
  JournalEntry,
  JournalPart,
  VersionInfo,
} from '../types/swyx';

// The preload exposes all ComSocket methods as Promise<unknown> because the
// bridge answers JSON-RPC without a static schema. These guards narrow the
// runtime payload back into the typed shapes declared in shared/types.
function asPhoneBookReply(payload: unknown): PhoneBookEntry[] {
  if (!payload || typeof payload !== 'object') return [];
  const root = payload as Partial<PhoneBookReply> & { entries?: unknown };
  if (Array.isArray(root.entries)) return root.entries as PhoneBookEntry[];
  // Some bridge variants return a bare array — accept that too.
  if (Array.isArray(payload)) return payload as PhoneBookEntry[];
  return [];
}

function asJournal(payload: unknown): JournalEntry[] {
  if (Array.isArray(payload)) return payload as JournalEntry[];
  return [];
}

function asVersionInfo(payload: unknown): VersionInfo | null {
  if (!payload || typeof payload !== 'object') return null;
  return payload as VersionInfo;
}

// Bridge part codes accepted by cs.getCallJournal (0=all, 1=missed, 2=outgoing, 3=incoming)
const PART_CODE: Record<JournalPart, number> = {
  all: 0,
  missed: 1,
  outgoing: 2,
  incoming: 3,
};

export interface UseComSocketResult {
  phoneBook: PhoneBookEntry[];
  journal: JournalEntry[];
  versionInfo: VersionInfo | null;
  /** True once any ComSocket method has resolved successfully. */
  available: boolean;
  loading: boolean;
  error: string | null;
  refreshPhoneBook: () => Promise<void>;
  refreshJournal: (part?: JournalPart) => Promise<void>;
}

/**
 * Reads rich data from the ComSocket (SignalR) bridge. Every call is wrapped
 * so that a missing/offline bridge degrades gracefully to `available: false`,
 * letting callers fall back to the legacy COM API.
 */
export function useComSocket(): UseComSocketResult {
  const [phoneBook, setPhoneBook] = useState<PhoneBookEntry[]>([]);
  const [journal, setJournal] = useState<JournalEntry[]>([]);
  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(null);
  const [available, setAvailable] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const apiRef = useRef(window.swyxApi);
  // Keep `available` from flipping back to false if a later refresh fails —
  // once we know the bridge works we don't want the UI to thrash.
  const markedAvailable = useRef(false);

  const refreshPhoneBook = useCallback(async () => {
    try {
      const payload = await apiRef.current.csGetPhoneBook();
      const entries = asPhoneBookReply(payload);
      setPhoneBook(entries);
      if (entries.length > 0 || !markedAvailable.current) {
        markedAvailable.current = true;
        setAvailable(true);
      }
      setError(null);
    } catch (err) {
      if (!markedAvailable.current) {
        setAvailable(false);
        setError(err instanceof Error ? err.message : 'ComSocket nicht verfügbar');
      }
    }
  }, []);

  const refreshJournal = useCallback(async (part: JournalPart = 'all') => {
    try {
      const payload = await apiRef.current.csGetCallJournal(PART_CODE[part]);
      setJournal(asJournal(payload));
      if (!markedAvailable.current) {
        markedAvailable.current = true;
        setAvailable(true);
      }
      setError(null);
    } catch (err) {
      if (!markedAvailable.current) {
        setAvailable(false);
        setError(err instanceof Error ? err.message : 'ComSocket nicht verfügbar');
      }
    }
  }, []);

  const fetchVersionInfo = useCallback(async () => {
    try {
      const info = asVersionInfo(await apiRef.current.csGetVersionInfo());
      setVersionInfo(info);
      if (info) {
        markedAvailable.current = true;
        setAvailable(true);
      }
    } catch {
      // Version is informational — never overrides availability.
    }
  }, []);

  // Initial load.
  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      await Promise.all([refreshPhoneBook(), refreshJournal('all'), fetchVersionInfo()]);
      if (!cancelled) setLoading(false);
    })();
    return () => {
      cancelled = true;
    };
  }, [refreshPhoneBook, refreshJournal, fetchVersionInfo]);

  // Auto-refresh phonebook whenever the bridge pushes a userDataChanged event
  // (presence flips, name changes, colleagues added/removed).
  useEffect(() => {
    const unsub = window.swyxApi.onCsUserDataChanged(() => {
      void refreshPhoneBook();
    });
    return () => unsub();
  }, [refreshPhoneBook]);

  return {
    phoneBook,
    journal,
    versionInfo,
    available,
    loading,
    error,
    refreshPhoneBook,
    refreshJournal,
  };
}

export default useComSocket;
