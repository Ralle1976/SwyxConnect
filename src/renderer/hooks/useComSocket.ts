import { usePhoneBookStore } from '../stores/usePhoneBookStore';
import type { JournalPart, PhoneBookEntry, JournalEntry, VersionInfo } from '../types/swyx';

export interface UseComSocketResult {
  phoneBook: PhoneBookEntry[];
  journal: JournalEntry[];
  versionInfo: VersionInfo | null;
  available: boolean;
  loading: boolean;
  error: string | null;
  refreshPhoneBook: () => Promise<void>;
  refreshJournal: (part?: JournalPart) => Promise<void>;
}

/**
 * Thin wrapper around the shared usePhoneBookStore so all consumers
 * (PresenceView, HistoryView, CallcenterDashboard) see the same data
 * instead of each maintaining independent state.
 */
export function useComSocket(): UseComSocketResult {
  const phoneBook = usePhoneBookStore((s) => s.phoneBook);
  const journal = usePhoneBookStore((s) => s.journal);
  const available = usePhoneBookStore((s) => s.available);
  const error = usePhoneBookStore((s) => s.error);
  const refreshPhoneBook = usePhoneBookStore((s) => s.refreshPhoneBook);
  const refreshJournal = usePhoneBookStore((s) => s.refreshJournal);

  return {
    phoneBook,
    journal,
    versionInfo: null,
    available,
    loading: false,
    error,
    refreshPhoneBook,
    refreshJournal,
  };
}

export default useComSocket;
