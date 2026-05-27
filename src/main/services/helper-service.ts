import fs from "node:fs";
import path from "node:path";
import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process";
import { EventEmitter } from "node:events";
import readline from "node:readline";
import { app } from "electron";
import { createDisconnectedSnapshot, type MediaSnapshot } from "../../shared/media";
import type { AppSettings } from "../../shared/settings";

type HelperInboundMessage =
  | { type: "ready" }
  | { type: "snapshot"; snapshot: MediaSnapshot }
  | { type: "error"; message: string };

type HelperOutboundMessage =
  | { type: "configure"; playerWhitelist: string[] }
  | { type: "command"; command: "playPause" | "next" | "previous" | "adjustVolume"; delta?: number };

export class MediaHelperService extends EventEmitter {
  private childProcess: ChildProcessWithoutNullStreams | null = null;
  private lineReader: readline.Interface | null = null;
  private snapshot: MediaSnapshot = createDisconnectedSnapshot("Windows Helper 未就绪。");

  getSnapshot(): MediaSnapshot {
    return this.snapshot;
  }

  async start(settings: AppSettings): Promise<void> {
    const executablePath = resolveHelperExecutable();

    if (!executablePath) {
      this.setSnapshot(createDisconnectedSnapshot("未找到 Windows Helper，可先构建 native-helper。"));
      return;
    }

    this.childProcess = spawn(executablePath, [], {
      stdio: ["pipe", "pipe", "pipe"]
    });

    this.lineReader = readline.createInterface({
      input: this.childProcess.stdout
    });

    this.lineReader.on("line", (line) => {
      this.handleLine(line);
    });

    this.childProcess.stderr.on("data", (chunk) => {
      const message = chunk.toString().trim();

      if (message) {
        this.setErrorState(message);
      }
    });

    this.childProcess.on("exit", (code) => {
      this.setSnapshot(createDisconnectedSnapshot(`Windows Helper 已退出，退出码 ${code ?? "unknown"}。`));
      this.disposeProcess();
    });

    this.configure(settings.playerWhitelist);
  }

  stop(): void {
    this.disposeProcess();
  }

  configure(playerWhitelist: string[]): void {
    this.send({
      type: "configure",
      playerWhitelist
    });
  }

  playPause(): void {
    this.send({ type: "command", command: "playPause" });
  }

  next(): void {
    this.send({ type: "command", command: "next" });
  }

  previous(): void {
    this.send({ type: "command", command: "previous" });
  }

  adjustVolume(delta: number): void {
    this.send({ type: "command", command: "adjustVolume", delta });
  }

  private handleLine(line: string): void {
    try {
      const message = JSON.parse(line) as HelperInboundMessage;

      if (message.type === "snapshot") {
        this.setSnapshot(message.snapshot);
      }

      if (message.type === "error") {
        this.setErrorState(message.message);
      }
    } catch {
      this.setErrorState("Windows Helper 返回了无法解析的数据。");
    }
  }

  private send(message: HelperOutboundMessage): void {
    if (!this.childProcess || this.childProcess.killed) {
      return;
    }

    this.childProcess.stdin.write(`${JSON.stringify(message)}\n`);
  }

  private setSnapshot(snapshot: MediaSnapshot): void {
    this.snapshot = {
      ...snapshot,
      lastUpdatedAt: snapshot.lastUpdatedAt || new Date().toISOString()
    };
    this.emit("snapshot", this.snapshot);
  }

  private setErrorState(message: string): void {
    if (this.snapshot.track) {
      this.setSnapshot({
        ...this.snapshot,
        errorMessage: message
      });
      return;
    }

    this.setSnapshot(createDisconnectedSnapshot(message));
  }

  private disposeProcess(): void {
    this.lineReader?.close();
    this.lineReader = null;

    if (this.childProcess && !this.childProcess.killed) {
      this.childProcess.kill();
    }

    this.childProcess = null;
  }
}

function resolveHelperExecutable(): string | null {
  const candidatePaths = [
    path.join(process.cwd(), "native-helper", "MusicPixelPet.Helper", "bin", "Release", "net8.0-windows10.0.19041.0", "MusicPixelPet.Helper.exe"),
    path.join(app.getAppPath(), "native-helper", "MusicPixelPet.Helper", "bin", "Release", "net8.0-windows10.0.19041.0", "MusicPixelPet.Helper.exe"),
    path.join(process.resourcesPath, "helper", "MusicPixelPet.Helper.exe"),
    path.join(path.dirname(process.execPath), "helper", "MusicPixelPet.Helper.exe")
  ];

  return candidatePaths.find((candidatePath) => fs.existsSync(candidatePath)) ?? null;
}
