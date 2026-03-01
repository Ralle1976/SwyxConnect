import React, { useState, useEffect } from 'react';
import { X, Plus, Trash2 } from 'lucide-react';
import { useLocalContactStore, LocalContact } from '../../stores/useLocalContactStore';

interface AddContactDialogProps {
  /** Wenn gesetzt, wird der Dialog im Bearbeitungsmodus geöffnet */
  editContact?: LocalContact;
  /** Vorausgefüllte Rufnummer (z.B. aus Anrufliste) */
  initialNumber?: string;
  onClose: () => void;
}

export default function AddContactDialog({
  editContact,
  initialNumber,
  onClose,
}: AddContactDialogProps): React.JSX.Element {
  const addContact = useLocalContactStore((s) => s.addContact);
  const updateContact = useLocalContactStore((s) => s.updateContact);

  const isEditing = editContact !== undefined;

  const [name, setName] = useState(editContact?.name ?? '');
  const [numbers, setNumbers] = useState<string[]>(
    editContact?.numbers.length
      ? editContact.numbers
      : initialNumber
        ? [initialNumber]
        : ['']
  );
  const [email, setEmail] = useState(editContact?.email ?? '');
  const [company, setCompany] = useState(editContact?.company ?? '');
  const [notes, setNotes] = useState(editContact?.notes ?? '');
  const [errors, setErrors] = useState<Record<string, string>>({});

  // Klick außerhalb schließt Dialog
  function handleBackdropClick(e: React.MouseEvent<HTMLDivElement>): void {
    if (e.target === e.currentTarget) {
      onClose();
    }
  }

  function handleAddNumber(): void {
    setNumbers((prev) => [...prev, '']);
  }

  function handleRemoveNumber(index: number): void {
    setNumbers((prev) => prev.filter((_, i) => i !== index));
  }

  function handleNumberChange(index: number, value: string): void {
    setNumbers((prev) => prev.map((n, i) => (i === index ? value : n)));
  }

  function validate(): boolean {
    const newErrors: Record<string, string> = {};
    if (!name.trim()) {
      newErrors.name = 'Name ist erforderlich.';
    }
    const validNumbers = numbers.filter((n) => n.trim());
    if (validNumbers.length === 0) {
      newErrors.numbers = 'Mindestens eine Rufnummer ist erforderlich.';
    }
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  }

  function handleSave(): void {
    if (!validate()) return;

    const payload = {
      name: name.trim(),
      numbers: numbers.filter((n) => n.trim()).map((n) => n.trim()),
      email: email.trim() || undefined,
      company: company.trim() || undefined,
      notes: notes.trim() || undefined,
      isFavorite: editContact?.isFavorite ?? false,
    };

    if (isEditing && editContact) {
      updateContact(editContact.id, payload);
    } else {
      addContact(payload);
    }
    onClose();
  }

  // Escape schließt Dialog
  useEffect(() => {
    function handleKeyDown(e: KeyboardEvent): void {
      if (e.key === 'Escape') onClose();
    }
    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [onClose]);

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={handleBackdropClick}
    >
      <div className="relative w-full max-w-md mx-4 bg-white dark:bg-zinc-900 rounded-2xl border border-zinc-200 dark:border-zinc-700 shadow-2xl">
        {/* Header */}
        <div className="flex items-center justify-between px-5 pt-5 pb-4 border-b border-zinc-200 dark:border-zinc-800">
          <h2 className="text-base font-semibold text-zinc-900 dark:text-zinc-100">
            {isEditing ? 'Kontakt bearbeiten' : 'Neuer Kontakt'}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="p-1.5 rounded-md text-zinc-400 hover:text-zinc-700 dark:hover:text-zinc-200 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
            title="Schließen"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Form */}
        <div className="px-5 py-4 space-y-4 max-h-[70vh] overflow-y-auto">
          {/* Name */}
          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Name <span className="text-red-500">*</span>
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="Vorname Nachname"
              className={`w-full px-3 py-2 text-sm rounded-lg border bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-500 outline-none transition-colors
                ${errors.name
                  ? 'border-red-400 focus:border-red-500'
                  : 'border-zinc-200 dark:border-zinc-700 focus:border-blue-500 dark:focus:border-blue-400'
                }`}
            />
            {errors.name && (
              <p className="mt-1 text-xs text-red-500">{errors.name}</p>
            )}
          </div>

          {/* Telefonnummern */}
          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Telefonnummer(n) <span className="text-red-500">*</span>
            </label>
            <div className="space-y-2">
              {numbers.map((num, index) => (
                <div key={index} className="flex items-center gap-2">
                  <input
                    type="tel"
                    value={num}
                    onChange={(e) => handleNumberChange(index, e.target.value)}
                    placeholder="+49 89 123456"
                    className={`flex-1 px-3 py-2 text-sm rounded-lg border bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-500 outline-none font-mono transition-colors
                      ${errors.numbers && index === 0
                        ? 'border-red-400 focus:border-red-500'
                        : 'border-zinc-200 dark:border-zinc-700 focus:border-blue-500 dark:focus:border-blue-400'
                      }`}
                  />
                  {numbers.length > 1 && (
                    <button
                      type="button"
                      onClick={() => handleRemoveNumber(index)}
                      className="p-1.5 rounded-md text-zinc-400 hover:text-red-500 hover:bg-red-50 dark:hover:bg-red-950/30 transition-colors"
                      title="Nummer entfernen"
                    >
                      <Trash2 className="w-4 h-4" />
                    </button>
                  )}
                </div>
              ))}
            </div>
            {errors.numbers && (
              <p className="mt-1 text-xs text-red-500">{errors.numbers}</p>
            )}
            <button
              type="button"
              onClick={handleAddNumber}
              className="mt-2 flex items-center gap-1.5 text-xs text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 transition-colors"
            >
              <Plus className="w-3.5 h-3.5" />
              Weitere Nummer hinzufügen
            </button>
          </div>

          {/* E-Mail */}
          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              E-Mail
            </label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="name@beispiel.de"
              className="w-full px-3 py-2 text-sm rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-500 focus:border-blue-500 dark:focus:border-blue-400 outline-none transition-colors"
            />
          </div>

          {/* Firma */}
          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Firma
            </label>
            <input
              type="text"
              value={company}
              onChange={(e) => setCompany(e.target.value)}
              placeholder="Firmenname"
              className="w-full px-3 py-2 text-sm rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-500 focus:border-blue-500 dark:focus:border-blue-400 outline-none transition-colors"
            />
          </div>

          {/* Notizen */}
          <div>
            <label className="block text-xs font-medium text-zinc-700 dark:text-zinc-300 mb-1">
              Notizen
            </label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              placeholder="Optionale Notizen…"
              rows={3}
              className="w-full px-3 py-2 text-sm rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 text-zinc-900 dark:text-zinc-100 placeholder-zinc-400 dark:placeholder-zinc-500 focus:border-blue-500 dark:focus:border-blue-400 outline-none transition-colors resize-none"
            />
          </div>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-end gap-2 px-5 py-4 border-t border-zinc-200 dark:border-zinc-800">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium rounded-lg text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
          >
            Abbrechen
          </button>
          <button
            type="button"
            onClick={handleSave}
            className="px-4 py-2 text-sm font-medium rounded-lg bg-blue-500 hover:bg-blue-600 text-white transition-colors"
          >
            {isEditing ? 'Speichern' : 'Kontakt erstellen'}
          </button>
        </div>
      </div>
    </div>
  );
}
