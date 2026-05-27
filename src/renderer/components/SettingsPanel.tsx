import { useEffect, useState } from "react";
import type { AppSettings, ControlBarDisplayMode, MusicRule, RuleMatchField, RulePetMode } from "@shared/settings";

interface SettingsPanelProps {
  settings: AppSettings;
  onClose: () => void;
  onSave: (patch: Partial<AppSettings>) => Promise<void>;
}

function createRule(): MusicRule {
  return {
    id: crypto.randomUUID(),
    keyword: "",
    field: "any",
    mode: "default"
  };
}

export function SettingsPanel({ settings, onClose, onSave }: SettingsPanelProps) {
  const [draft, setDraft] = useState<AppSettings>(settings);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    setDraft(settings);
  }, [settings]);

  const updateRule = <TKey extends keyof MusicRule>(ruleId: string, key: TKey, value: MusicRule[TKey]) => {
    setDraft((current) => ({
      ...current,
      musicRules: current.musicRules.map((rule) => (rule.id === ruleId ? { ...rule, [key]: value } : rule))
    }));
  };

  const save = async () => {
    setIsSaving(true);

    try {
      await onSave({
        skinId: draft.skinId,
        wheelVolumeEnabled: draft.wheelVolumeEnabled,
        playerWhitelist: draft.playerWhitelist,
        musicRules: draft.musicRules.filter((rule) => rule.keyword.trim()),
        autoLaunch: draft.autoLaunch,
        alwaysOnTop: draft.alwaysOnTop,
        controlBarMode: draft.controlBarMode
      });
      onClose();
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <section className="settings-panel no-drag">
      <div className="settings-header">
        <strong>桌宠设置</strong>
        <button className="settings-close" onClick={onClose} aria-label="关闭">
          关闭
        </button>
      </div>

      <label className="settings-field">
        <span>皮肤</span>
        <select value={draft.skinId} onChange={(event) => setDraft((current) => ({ ...current, skinId: event.target.value }))}>
          <option value="default">default</option>
        </select>
      </label>

      <label className="settings-inline">
        <input
          type="checkbox"
          checked={draft.alwaysOnTop}
          onChange={(event) => setDraft((current) => ({ ...current, alwaysOnTop: event.target.checked }))}
        />
        <span>始终置顶</span>
      </label>

      <label className="settings-inline">
        <input
          type="checkbox"
          checked={draft.autoLaunch}
          onChange={(event) => setDraft((current) => ({ ...current, autoLaunch: event.target.checked }))}
        />
        <span>开机自启动</span>
      </label>

      <label className="settings-inline">
        <input
          type="checkbox"
          checked={draft.wheelVolumeEnabled}
          onChange={(event) => setDraft((current) => ({ ...current, wheelVolumeEnabled: event.target.checked }))}
        />
        <span>允许滚轮调节音量</span>
      </label>

      <label className="settings-field">
        <span>控制条显示方式</span>
        <select
          value={draft.controlBarMode}
          onChange={(event) =>
            setDraft((current) => ({
              ...current,
              controlBarMode: event.target.value as ControlBarDisplayMode
            }))
          }
        >
          <option value="hover">悬停显示</option>
          <option value="always">始终显示</option>
        </select>
      </label>

      <label className="settings-field">
        <span>目标播放器白名单</span>
        <input
          value={draft.playerWhitelist.join(", ")}
          onChange={(event) =>
            setDraft((current) => ({
              ...current,
              playerWhitelist: event.target.value
                .split(",")
                .map((value) => value.trim().toLowerCase())
                .filter(Boolean)
            }))
          }
        />
      </label>

      <div className="settings-rules">
        <div className="settings-rules-header">
          <strong>歌曲规则</strong>
          <button
            className="control-button"
            onClick={() =>
              setDraft((current) => ({
                ...current,
                musicRules: [...current.musicRules, createRule()]
              }))
            }
          >
            添加
          </button>
        </div>

        {draft.musicRules.length === 0 ? <span className="settings-empty">未添加规则，播放中的歌曲默认使用播放动画。</span> : null}

        {draft.musicRules.map((rule) => (
          <div className="rule-row" key={rule.id}>
            <input
              placeholder="关键词"
              value={rule.keyword}
              onChange={(event) => updateRule(rule.id, "keyword", event.target.value)}
            />
            <select value={rule.field} onChange={(event) => updateRule(rule.id, "field", event.target.value as RuleMatchField)}>
              <option value="any">任意字段</option>
              <option value="title">歌曲名</option>
              <option value="artist">歌手</option>
              <option value="album">专辑</option>
            </select>
            <select value={rule.mode} onChange={(event) => updateRule(rule.id, "mode", event.target.value as RulePetMode)}>
              <option value="default">默认播放</option>
              <option value="energetic">活跃动画</option>
              <option value="sleepy">安静动画</option>
            </select>
            <button
              className="control-button danger"
              onClick={() =>
                setDraft((current) => ({
                  ...current,
                  musicRules: current.musicRules.filter((currentRule) => currentRule.id !== rule.id)
                }))
              }
            >
              删除
            </button>
          </div>
        ))}
      </div>

      <div className="settings-actions">
        <button className="control-button" onClick={onClose}>
          取消
        </button>
        <button className="control-button is-active" onClick={save} disabled={isSaving}>
          {isSaving ? "保存中..." : "保存"}
        </button>
      </div>
    </section>
  );
}
