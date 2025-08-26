import customtkinter as ctk
import threading
import time
import os
import sys
import ctypes
import json
from pathlib import Path
from datetime import date
from tkinter import messagebox, filedialog
from pynput import mouse, keyboard
import pygame

# ===== 路徑：專案根與資源 =====
# 專案根（src 的上一層）
BASE_DIR = Path(__file__).resolve().parent.parent
ASSETS_DIR = BASE_DIR / "assets"
SOUNDS_DIR = ASSETS_DIR / "sounds"

# PyInstaller 相容：打包後資料會被展開在 sys._MEIPASS
def resource_path(rel_path: str) -> str:
    """
    回傳在開發/打包兩種情況可用的資源路徑。
    若是打包後執行，rel_path 會接在 sys._MEIPASS。
    """
    base = Path(getattr(sys, "_MEIPASS", BASE_DIR))
    return str((base / rel_path).resolve())

# 內建音檔（先嘗試 assets/sounds，相容打包時也可用）
BUILTIN_MUSIC = str((SOUNDS_DIR / "start.mp3").resolve())
BUILTIN_SOUND = str((SOUNDS_DIR / "rest.wav").resolve())

# 若使用者真的以 --add-data 打包到根目錄，也能 fallback 找到：
if not os.path.exists(BUILTIN_MUSIC):
    maybe = resource_path("assets/sounds/start.mp3")
    if os.path.exists(maybe): BUILTIN_MUSIC = maybe
if not os.path.exists(BUILTIN_SOUND):
    maybe = resource_path("assets/sounds/rest.wav")
    if os.path.exists(maybe): BUILTIN_SOUND = maybe

# ===== 使用者層級資料夾（設定 / 統計） =====
def app_data_dir() -> Path:
    """Windows: %APPDATA%\\TwentyTimer；其他：~/.twentytimer"""
    if sys.platform.startswith("win"):
        root = Path(os.environ.get("APPDATA", str(Path.home() / "AppData/Roaming")))
        d = root / "TwentyTimer"
    else:
        d = Path.home() / ".twentytimer"
    d.mkdir(parents=True, exist_ok=True)
    return d

STATS_FILE  = str(app_data_dir() / "stats.json")
CONFIG_FILE = str(app_data_dir() / "config.json")

# ===== 基本設定 =====
WORK_TIME = 20 * 60        # 20 分鐘工作
REST_TIME = 30             # 30 秒休息
IDLE_LIMIT = 10            # 無動作超過 10 秒就暫停

# ===== 初始化 pygame mixer（無聲卡環境容錯） =====
try:
    pygame.mixer.init()
    _AUDIO_OK = True
except Exception as e:
    print("[提示] 無法初始化音訊裝置，音效將停用：", e)
    _AUDIO_OK = False

# ====== 音效工具 ======
def play_music_limited(path, duration=30):
    """播放音樂 (mp3/wav/ogg)，限定秒數，自動停止；若檔案不存在會忽略。"""
    if not _AUDIO_OK:
        return
    try:
        if not path or not os.path.exists(path):
            print(f"[提示] 音樂檔不存在：{path}")
            return
        pygame.mixer.music.stop()
        pygame.mixer.music.load(path)
        pygame.mixer.music.play()
        threading.Timer(duration, stop_music).start()
    except Exception as e:
        print("播放音樂失敗:", e)

def stop_music():
    if not _AUDIO_OK:
        return
    try:
        pygame.mixer.music.stop()
    except Exception:
        pass

def play_sound(path):
    """播放一次短提示音；若檔案不存在會忽略。"""
    if not _AUDIO_OK:
        return
    try:
        if not path or not os.path.exists(path):
            print(f"[提示] 提示音檔不存在：{path}")
            return
        snd = pygame.mixer.Sound(path)
        snd.play()
    except Exception as e:
        print("音效播放失敗:", e)

# ====== 統計功能 ======
def load_stats():
    if os.path.exists(STATS_FILE):
        try:
            with open(STATS_FILE, "r", encoding="utf-8") as f:
                return json.load(f)
        except Exception:
            return {}
    return {}

def save_stats(stats):
    try:
        with open(STATS_FILE, "w", encoding="utf-8") as f:
            json.dump(stats, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print("儲存統計失敗：", e)

def add_work_cycle():
    stats = load_stats()
    today = str(date.today())
    stats[today] = stats.get(today, 0) + 1
    save_stats(stats)
    return stats[today]

def get_today_count():
    stats = load_stats()
    today = str(date.today())
    return stats.get(today, 0)

def clear_stats():
    save_stats({})
    return 0

# ====== 設定檔 ======
def load_config():
    """讀設定；若無則給預設（指向內建音檔）。"""
    cfg = {
        "music_path": BUILTIN_MUSIC,  # 預設用內建
        "sound_path": BUILTIN_SOUND,  # 預設用內建
        "theme": "system",            # "system" / "light" / "dark"
        "always_on_top": True
    }
    if os.path.exists(CONFIG_FILE):
        try:
            with open(CONFIG_FILE, "r", encoding="utf-8") as f:
                data = json.load(f)
                for k in list(cfg.keys()):
                    if k in data:
                        cfg[k] = data[k]
        except Exception:
            pass
    return cfg

def save_config(cfg):
    try:
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(cfg, f, ensure_ascii=False, indent=2)
    except Exception as e:
        print("儲存設定失敗：", e)

# ---- 取得「實際使用」的音檔路徑（帶 fallback 到內建）----
def resolve_music_path(cfg) -> str:
    p = cfg.get("music_path") or ""
    return p if os.path.exists(p) else BUILTIN_MUSIC

def resolve_sound_path(cfg) -> str:
    p = cfg.get("sound_path") or ""
    return p if os.path.exists(p) else BUILTIN_SOUND

# ====== 判斷是否全螢幕（Windows） ======
def is_foreground_fullscreen():
    if not sys.platform.startswith("win"):
        return False
    try:
        user32 = ctypes.windll.user32
        hwnd = user32.GetForegroundWindow()
        if not hwnd:
            return False

        class RECT(ctypes.Structure):
            _fields_ = [("left", ctypes.c_long),
                        ("top", ctypes.c_long),
                        ("right", ctypes.c_long),
                        ("bottom", ctypes.c_long)]
        rect = RECT()
        if not user32.GetWindowRect(hwnd, ctypes.byref(rect)):
            return False

        MONITOR_DEFAULTTONEAREST = 2
        monitor = user32.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST)

        class MONITORINFO(ctypes.Structure):
            _fields_ = [("cbSize", ctypes.c_ulong),
                        ("rcMonitor", RECT),
                        ("rcWork", RECT),
                        ("dwFlags", ctypes.c_ulong)]
        mi = MONITORINFO()
        mi.cbSize = ctypes.sizeof(MONITORINFO)
        if not user32.GetMonitorInfoW(monitor, ctypes.byref(mi)):
            return False

        tol = 2
        full = (
            abs(rect.left   - mi.rcMonitor.left)   <= tol and
            abs(rect.top    - mi.rcMonitor.top)    <= tol and
            abs(rect.right  - mi.rcMonitor.right)  <= tol and
            abs(rect.bottom - mi.rcMonitor.bottom) <= tol
        )
        return full
    except Exception:
        return False

# ====== 主程式（UI） ======
class TwentyTimerApp:
    def __init__(self, root):
        self.root = root
        self.root.title("20-20-20休息提醒")
        self.root.geometry("390x420")
        self.root.minsize(320, 280)
        self.root.resizable(True, True)

        try:
            ctypes.windll.shell32.SetCurrentProcessExplicitAppUserModelID("20-20-20.Timer")
        except Exception:
            pass

        # 載入設定 / 主題
        self.cfg = load_config()
        self.apply_theme(self.cfg.get("theme", "system"))

        # 狀態
        self.mode = "工作中"
        self.remaining = WORK_TIME
        self.is_running = True
        self.last_mouse_time = time.time()
        self.last_key_time = time.time()

        # ===== 頂部：時間 + 視圖切換 =====
        topbar = ctk.CTkFrame(root)
        topbar.pack(fill="x", padx=12, pady=(8, 2))

        self.label = ctk.CTkLabel(topbar, text="", font=("Arial", 22))
        self.label.pack(side="left", padx=(0, 8))

        self.view_btn = ctk.CTkButton(topbar, text="切換為極簡模式", width=120, command=self.toggle_view)
        self.view_btn.pack(side="right")

        # ===== 完整模式容器 =====
        self.full_frame = ctk.CTkFrame(root)
        self.full_frame.pack(fill="both", expand=True, padx=12, pady=6)

        # 今日完成
        self.stats_label = ctk.CTkLabel(self.full_frame, text=f"今天完成：{get_today_count()} 輪", font=("Arial", 14))
        self.stats_label.pack(pady=4, anchor="w")

        # 置頂切換
        pin_frame = ctk.CTkFrame(self.full_frame)
        pin_frame.pack(pady=4, fill="x")
        self.pin_switch = ctk.CTkSwitch(pin_frame, text="釘選在最上層", command=self.on_toggle_topmost)
        self.pin_switch.pack(side="left", padx=6, pady=6)
        if self.cfg.get("always_on_top", True):
            self.pin_switch.select()
        self.root.attributes("-topmost", bool(self.cfg.get("always_on_top", True)))

        # 主題切換
        theme_frame = ctk.CTkFrame(self.full_frame)
        theme_frame.pack(pady=6, fill="x")
        ctk.CTkLabel(theme_frame, text="主題：").pack(side="left", padx=6, pady=6)
        self.theme_menu = ctk.CTkOptionMenu(theme_frame, values=["跟隨系統", "亮色", "暗色"], command=self.on_theme_change)
        self.theme_menu.pack(side="left", padx=6, pady=6)
        mode_map_r = {"system": "跟隨系統", "light": "亮色", "dark": "暗色"}
        self.theme_menu.set(mode_map_r.get(self.cfg["theme"], "跟隨系統"))

        # 檔案選擇
        file_frame = ctk.CTkFrame(self.full_frame)
        file_frame.pack(pady=6, fill="x")
        ctk.CTkLabel(file_frame, text="音檔：").grid(row=0, column=0, padx=6, pady=6, sticky="w")
        self.btn_music = ctk.CTkButton(file_frame, text="選擇休息音樂 (A)", command=self.choose_music)
        self.btn_music.grid(row=0, column=1, padx=6, pady=6, sticky="w")
        self.music_label = ctk.CTkLabel(file_frame, text=os.path.basename(resolve_music_path(self.cfg)))
        self.music_label.grid(row=0, column=2, padx=6, pady=6, sticky="w")

        self.btn_sound = ctk.CTkButton(file_frame, text="選擇提示音 (B)", command=self.choose_sound)
        self.btn_sound.grid(row=1, column=1, padx=6, pady=6, sticky="w")
        self.sound_label = ctk.CTkLabel(file_frame, text=os.path.basename(resolve_sound_path(self.cfg)))
        self.sound_label.grid(row=1, column=2, padx=6, pady=6, sticky="w")

        # 測試音效
        test_frame = ctk.CTkFrame(self.full_frame)
        test_frame.pack(pady=6, fill="x")
        self.test_music_btn = ctk.CTkButton(test_frame, text="▶ 測試播放 A 音樂 (5秒)",
                                            command=lambda: play_music_limited(resolve_music_path(self.cfg), 5))
        self.test_music_btn.grid(row=0, column=0, padx=6, pady=6)
        self.test_sound_btn = ctk.CTkButton(test_frame, text="🔔 測試播放 B 提示音",
                                            command=lambda: play_sound(resolve_sound_path(self.cfg)))
        self.test_sound_btn.grid(row=0, column=1, padx=6, pady=6)

        # 歷史統計
        self.history_btn = ctk.CTkButton(self.full_frame, text="查看歷史統計", command=self.show_history)
        self.history_btn.pack(pady=6)

        # 透明度
        self.opacity_slider = ctk.CTkSlider(self.full_frame, from_=0.3, to=1.0, number_of_steps=70,
                                            command=self.change_opacity)
        self.opacity_slider.set(1.0)
        self.opacity_slider.pack(pady=(6, 2), fill="x")
        ctk.CTkLabel(self.full_frame, text="透明度").pack()

        # 關閉事件
        self.root.protocol("WM_DELETE_WINDOW", self.on_close)

        # 執行緒
        threading.Thread(target=self.update_timer, daemon=True).start()
        threading.Thread(target=self.monitor_activity, daemon=True).start()

        # 啟動播放提示音（已自動帶 fallback）
        play_sound(resolve_sound_path(self.cfg))

        # 視圖狀態（False=完整、True=極簡）
        self.is_mini = False

    # ---- 視圖切換（極簡只保留 topbar） ----
    def toggle_view(self):
        if self.is_mini:
            self.full_frame.pack(fill="both", expand=True, padx=12, pady=6)
            self.view_btn.configure(text="切換為極簡模式")
            self.root.geometry("390x420")
            self.root.minsize(320, 280)
            self.root.resizable(True, True)
            self.label.configure(font=("Arial", 22))
            self.is_mini = False
        else:
            self.full_frame.pack_forget()
            self.view_btn.configure(text="切換為完整模式")
            self.root.geometry("220x90")
            self.root.minsize(200, 80)
            self.root.resizable(False, False)
            self.label.configure(font=("Arial", 24))
            self.is_mini = True

    # ---- 置頂切換 ----
    def on_toggle_topmost(self):
        pinned = bool(self.pin_switch.get())
        self.root.attributes("-topmost", pinned)
        self.cfg["always_on_top"] = pinned
        save_config(self.cfg)

    # ---- 主題 ----
    def apply_theme(self, mode: str):
        mode_map = {"system": "System", "light": "Light", "dark": "Dark"}
        ctk.set_appearance_mode(mode_map.get(mode, "System"))
        self.cfg["theme"] = mode
        save_config(self.cfg)

    def on_theme_change(self, friendly_name: str):
        name_map = {"跟隨系統": "system", "亮色": "light", "暗色": "dark"}
        self.apply_theme(name_map.get(friendly_name, "system"))

    # ---- 音檔選擇 ----
    def choose_music(self):
        path = filedialog.askopenfilename(
            title="選擇休息音樂 (A)",
            filetypes=[("音訊檔", "*.mp3 *.wav *.ogg"), ("所有檔案", "*.*")]
        )
        if path:
            self.cfg["music_path"] = path
            save_config(self.cfg)
        self.music_label.configure(text=os.path.basename(resolve_music_path(self.cfg)))

    def choose_sound(self):
        path = filedialog.askopenfilename(
            title="選擇提示音 (B)",
            filetypes=[("音訊檔", "*.wav *.mp3 *.ogg"), ("所有檔案", "*.*")]
        )
        if path:
            self.cfg["sound_path"] = path
            save_config(self.cfg)
        self.sound_label.configure(text=os.path.basename(resolve_sound_path(self.cfg)))

    # ---- 其他 ----
    def change_opacity(self, val):
        self.root.attributes("-alpha", float(val))

    def on_close(self):
        self.root.destroy()

    def update_timer(self):
        while True:
            if self.is_running:
                mins, secs = divmod(self.remaining, 60)
                self.label.configure(text=f"{self.mode}：{mins:02d}:{secs:02d}")

                if self.remaining == 0:
                    if self.mode == "工作中":
                        # 開始休息：提示音 + 30秒音樂
                        play_sound(resolve_sound_path(self.cfg))
                        self.mode = "休息中"
                    # 設定下一段時間（這樣切換更乾淨）
                        self.remaining = REST_TIME
                        play_music_limited(resolve_music_path(self.cfg), REST_TIME)
                    else:
                        # 結束休息 → 回到工作
                        stop_music()
                        play_sound(resolve_sound_path(self.cfg))
                        self.mode = "工作中"
                        self.remaining = WORK_TIME
                        today_count = add_work_cycle()
                        self.stats_label.configure(text=f"今天完成：{today_count} 輪")
                else:
                    self.remaining -= 1
            else:
                self.label.configure(text=f"⏸ 已暫停 {self.mode}")
            time.sleep(1)

    def monitor_activity(self):
        def on_mouse_move(x, y):
            self.last_mouse_time = time.time()
        def on_key_press(key):
            self.last_key_time = time.time()

        mouse.Listener(on_move=on_mouse_move).start()
        keyboard.Listener(on_press=on_key_press).start()

        while True:
            now = time.time()
            idle = ((now - self.last_mouse_time > IDLE_LIMIT) and
                    (now - self.last_key_time  > IDLE_LIMIT))
            fullscreen = is_foreground_fullscreen()
            self.is_running = not (idle and (not fullscreen))
            time.sleep(1)

    def show_history(self):
        stats = load_stats()
        win = ctk.CTkToplevel(self.root)
        win.title("歷史統計")
        win.geometry("300x400")
        win.minsize(260, 260)

        if not stats:
            ctk.CTkLabel(win, text="目前沒有紀錄").pack(pady=16)
        else:
            for day, count in sorted(stats.items()):
                ctk.CTkLabel(win, text=f"{day}：{count} 輪", font=("Arial", 12)).pack(anchor="w", padx=18, pady=2)

        def do_clear():
            if messagebox.askyesno("確認", "確定要清除所有統計嗎？"):
                clear_stats()
                self.stats_label.configure(text="今天完成：0 輪")
                for w in win.winfo_children():
                    w.destroy()
                ctk.CTkLabel(win, text="紀錄已清除").pack(pady=20)

        ctk.CTkButton(win, text="清除全部紀錄", command=do_clear).pack(pady=10)


if __name__ == "__main__":
    ctk.set_appearance_mode("Dark")   # 實際會在 app 內被 config 覆蓋
    ctk.set_default_color_theme("blue")

    root = ctk.CTk()
    app = TwentyTimerApp(root)
    root.mainloop()
