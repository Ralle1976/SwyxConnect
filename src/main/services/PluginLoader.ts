import * as fs from 'fs';
import * as path from 'path';

export interface PluginManifest {
  name: string;
  version: string;
  description: string;
  main: string;
  author?: string;
  swyxApi?: string;
}

export interface LoadedPlugin {
  id: string;
  manifest: PluginManifest;
  directory: string;
  enabled: boolean;
  error?: string;
}

export class PluginLoader {
  private plugins: Map<string, LoadedPlugin> = new Map();
  private pluginsDirectory = '';

  setDirectory(dir: string): void {
    this.pluginsDirectory = dir;
  }

  scan(): LoadedPlugin[] {
    this.plugins.clear();

    if (!this.pluginsDirectory || !fs.existsSync(this.pluginsDirectory)) {
      return [];
    }

    let entries: string[];
    try {
      entries = fs.readdirSync(this.pluginsDirectory);
    } catch {
      return [];
    }

    for (const entry of entries) {
      const pluginDir = path.join(this.pluginsDirectory, entry);

      try {
        const stat = fs.statSync(pluginDir);
        if (!stat.isDirectory()) continue;

        const manifestPath = path.join(pluginDir, 'package.json');
        if (!fs.existsSync(manifestPath)) continue;

        const raw = fs.readFileSync(manifestPath, 'utf8');
        const manifest = JSON.parse(raw) as PluginManifest;

        if (!manifest.name || !manifest.version || !manifest.main) continue;

        const plugin: LoadedPlugin = {
          id: entry,
          manifest,
          directory: pluginDir,
          enabled: true,
        };

        this.plugins.set(entry, plugin);
      } catch (err) {
        const errorPlugin: LoadedPlugin = {
          id: entry,
          manifest: {
            name: entry,
            version: '0.0.0',
            description: '',
            main: '',
          },
          directory: path.join(this.pluginsDirectory, entry),
          enabled: false,
          error: err instanceof Error ? err.message : String(err),
        };
        this.plugins.set(entry, errorPlugin);
      }
    }

    return [...this.plugins.values()];
  }

  getPlugins(): LoadedPlugin[] {
    return [...this.plugins.values()];
  }

  getPlugin(id: string): LoadedPlugin | undefined {
    return this.plugins.get(id);
  }
}
