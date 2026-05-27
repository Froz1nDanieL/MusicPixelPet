import { Menu, Tray, nativeImage, type BrowserWindow, type NativeImage } from "electron";

interface TrayOptions {
  window: BrowserWindow;
  isAlwaysOnTop: () => boolean;
  onOpenSettings: () => void;
  onToggleAlwaysOnTop: () => void;
  onQuit: () => void;
}

export function createAppTray(options: TrayOptions): Tray {
  const tray = new Tray(createTrayIcon());

  const refreshMenu = () => {
    tray.setContextMenu(
      Menu.buildFromTemplate([
        {
          label: options.window.isVisible() ? "隐藏桌宠" : "显示桌宠",
          click: () => toggleWindowVisibility(options.window)
        },
        {
          label: options.isAlwaysOnTop() ? "取消置顶" : "始终置顶",
          click: options.onToggleAlwaysOnTop
        },
        {
          label: "打开设置",
          click: () => {
            ensureWindowVisible(options.window);
            options.onOpenSettings();
          }
        },
        {
          type: "separator"
        },
        {
          label: "退出",
          click: options.onQuit
        }
      ])
    );
  };

  tray.setToolTip("Music Pixel Pet");
  tray.on("click", () => {
    toggleWindowVisibility(options.window);
    refreshMenu();
  });

  refreshMenu();
  return tray;
}

function createTrayIcon(): NativeImage {
  const svg = `
    <svg xmlns="http://www.w3.org/2000/svg" width="32" height="32" viewBox="0 0 32 32">
      <rect width="32" height="32" rx="6" fill="#11261f"/>
      <rect x="7" y="8" width="6" height="6" fill="#f4f1dc"/>
      <rect x="19" y="8" width="6" height="6" fill="#f4f1dc"/>
      <rect x="10" y="19" width="12" height="4" fill="#e08e45"/>
      <rect x="13" y="14" width="6" height="3" fill="#4cd5b5"/>
    </svg>
  `;

  return nativeImage.createFromDataURL(`data:image/svg+xml;charset=UTF-8,${encodeURIComponent(svg)}`);
}

function toggleWindowVisibility(window: BrowserWindow): void {
  if (window.isVisible()) {
    window.hide();
    return;
  }

  ensureWindowVisible(window);
}

function ensureWindowVisible(window: BrowserWindow): void {
  window.show();
  window.focus();
}

