import React, { useState } from 'react';
import { useAuthStore } from '../../stores/useAuthStore';
import { AuthMode, AuthCredentials } from '../../types/swyx';

interface LoginFormData {
  server: string;
  backupServer: string;
  username: string;
  password: string;
  ctiMaster: boolean;
}

export function LoginView(): JSX.Element {
  const { login, status, error } = useAuthStore();
  const [formData, setFormData] = useState<LoginFormData>({
    server: '',
    backupServer: '',
    username: '',
    password: '',
    ctiMaster: false,
  });

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const credentials: AuthCredentials = {
      server: formData.server,
      backupServer: formData.backupServer || undefined,
      username: formData.username,
      password: formData.password,
      authMode: AuthMode.UsernamePassword,
      ctiMaster: formData.ctiMaster,
    };
    await login(credentials);
  };

  const isSubmitting = status === 'authenticating';

  return (
    <div className="flex h-screen w-full items-center justify-center bg-gray-100 dark:bg-gray-900">
      <div className="w-full max-w-md rounded-lg bg-white p-8 shadow-lg dark:bg-gray-800">
        <div className="mb-6 text-center">
          <h1 className="text-2xl font-bold text-gray-900 dark:text-white">
            SwyxConnect
          </h1>
          <p className="mt-2 text-sm text-gray-600 dark:text-gray-400">
            Bitte melden Sie sich an
          </p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label
              htmlFor="server"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300"
            >
              Server
            </label>
            <input
              id="server"
              type="text"
              required
              value={formData.server}
              onChange={(e) =>
                setFormData((prev) => ({ ...prev, server: e.target.value }))
              }
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="swyx-server.local"
            />
          </div>

          <div>
            <label
              htmlFor="backupServer"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300"
            >
              Backup Server (optional)
            </label>
            <input
              id="backupServer"
              type="text"
              value={formData.backupServer}
              onChange={(e) =>
                setFormData((prev) => ({ ...prev, backupServer: e.target.value }))
              }
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="backup-server.local"
            />
          </div>

          <div>
            <label
              htmlFor="username"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300"
            >
              Benutzername
            </label>
            <input
              id="username"
              type="text"
              required
              value={formData.username}
              onChange={(e) =>
                setFormData((prev) => ({ ...prev, username: e.target.value }))
              }
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="Benutzername"
            />
          </div>

          <div>
            <label
              htmlFor="password"
              className="block text-sm font-medium text-gray-700 dark:text-gray-300"
            >
              Passwort
            </label>
            <input
              id="password"
              type="password"
              required
              value={formData.password}
              onChange={(e) =>
                setFormData((prev) => ({ ...prev, password: e.target.value }))
              }
              className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-gray-900 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 dark:border-gray-600 dark:bg-gray-700 dark:text-white"
              placeholder="••••••••"
            />
          </div>

          <div className="flex items-center">
            <input
              id="ctiMaster"
              type="checkbox"
              checked={formData.ctiMaster}
              onChange={(e) =>
                setFormData((prev) => ({ ...prev, ctiMaster: e.target.checked }))
              }
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <label
              htmlFor="ctiMaster"
              className="ml-2 block text-sm text-gray-700 dark:text-gray-300"
            >
              Als CTI-Master anmelden
            </label>
          </div>

          {error && (
            <div className="rounded-md bg-red-50 p-3 dark:bg-red-900/20">
              <p className="text-sm text-red-800 dark:text-red-300">{error}</p>
            </div>
          )}

          <button
            type="submit"
            disabled={isSubmitting}
            className="flex w-full justify-center rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {isSubmitting ? 'Anmelden...' : 'Anmelden'}
          </button>
        </form>

        <div className="mt-6 text-center">
          <p className="text-xs text-gray-500 dark:text-gray-400">
            Erfordert Swyx Client SDK oder SwyxIt! Installation
          </p>
        </div>
      </div>
    </div>
  );
}
