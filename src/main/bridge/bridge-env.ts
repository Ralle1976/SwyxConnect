// Reads Swyx credentials from .env file for auto-login.
// Values are NEVER logged, exposed to renderer, or committed to git.
import * as fs from 'fs';
import * as path from 'path';
import { app } from 'electron';

export interface BridgeCredentials {
  server: string;
  publicServer: string;
  username: string;
  password: string;
  backupServer?: string;
  publicBackupServer?: string;
  authMode: number;
}

let cachedCredentials: BridgeCredentials | null | undefined;

/**
 * Reads credentials from .env file. Returns null if .env doesn't exist
 * or required keys are missing.
 *
 * The .env file must be in the project root (dev) or next to the executable (packaged).
 * Required keys: SWYX_USERNAME, SWYX_PASSWORD
 * Optional keys: SWYX_SERVER (default: read from .env), SWYX_PUBLIC_SERVER (default: read from .env)
 */
export function getBridgeCredentials(): BridgeCredentials | null {
  if (cachedCredentials !== undefined) return cachedCredentials;

  const envPath = findEnvFile();
  if (!envPath) {
    cachedCredentials = null;
    return null;
  }

  try {
    const content = fs.readFileSync(envPath, 'utf8');
    const env: Record<string, string> = {};
    for (const line of content.split('\n')) {
      const trimmed = line.trim();
      if (!trimmed || trimmed.startsWith('#')) continue;
      const eq = trimmed.indexOf('=');
      if (eq < 0) continue;
      env[trimmed.substring(0, eq).trim()] = trimmed.substring(eq + 1).trim();
    }

    const username = env.SWYX_USERNAME;
    const password = env.SWYX_PASSWORD;

    if (!username || !password) {
      cachedCredentials = null;
      return null;
    }

    cachedCredentials = {
      username,
      password,
      server: env.SWYX_SERVER || '',
      publicServer: env.SWYX_PUBLIC_SERVER || '',
      backupServer: env.SWYX_BACKUP_SERVER || '',
      publicBackupServer: env.SWYX_PUBLIC_BACKUP_SERVER || '',
      authMode: parseInt(env.SWYX_AUTH_MODE || '1', 10),
    };

    return cachedCredentials;
  } catch {
    cachedCredentials = null;
    return null;
  }
}

/**
 * Clears the credential cache. Call this when .env changes.
 */
export function clearCredentialCache(): void {
  cachedCredentials = undefined;
}

function findEnvFile(): string | null {
  // Dev mode: project root (app.getAppPath() = .../out/main/)
  const devRoot = path.resolve(app.getAppPath(), '..', '..');
  const devEnv = path.join(devRoot, '.env');

  // Packaged mode: next to the executable
  const packagedRoot = path.dirname(app.getPath('exe'));
  const packagedEnv = path.join(packagedRoot, '.env');

  // userData folder (AppData\Roaming\SwyxConnect) — writable without admin rights
  const userDataEnv = path.join(app.getPath('userData'), '.env');

  // Also check resources/ (where electron-builder copies extra files)
  const resourcesEnv = app.isPackaged
    ? path.join(process.resourcesPath, '.env')
    : null;

  // Priority: dev root > userData > packaged > resources
  const candidates = [devEnv, userDataEnv, packagedEnv];
  if (resourcesEnv) candidates.push(resourcesEnv);

  for (const candidate of candidates) {
    try {
      if (fs.existsSync(candidate)) return candidate;
    } catch {
      // ignore
    }
  }

  return null;
}
