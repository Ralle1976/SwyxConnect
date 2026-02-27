export interface IPlugin {
  id: string;
  name: string;
  version: string;
  description: string;
  author?: string;
  enabled: boolean;
  onLoad?: () => void;
  onUnload?: () => void;
}

export interface PluginRegistration {
  id: string;
  manifest: {
    name: string;
    version: string;
    description: string;
    main: string;
    author?: string;
  };
  directory: string;
  enabled: boolean;
  error?: string;
}
