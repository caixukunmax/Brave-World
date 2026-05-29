import subprocess
import time
import sys
import os
import shutil

PROJECT_PATH = r"c:\code\tslua2\clinetcsharp"
GODOT_EXE = r"D:\Program Files (x86)\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64.exe"
SCREENSHOT_DIR = os.path.join(PROJECT_PATH, "screenshots", "auto_test_multi_zoom")

# Godot 4.x user:// 在 Windows 上的实际路径
GODOT_USER_DIR = os.path.expandvars(r"%APPDATA%\Godot\app_userdata\clinetcsharp")

# 多个 zoom 级别对应的截图文件名（与 C# 代码中生成的一致）
ZOOM_LEVELS = ["1.00", "0.70", "0.50", "0.40", "0.30", "0.20"]

def log(msg):
    print(f"[AutoTest] {msg}")
    sys.stdout.flush()


def is_explorer_running():
    try:
        result = subprocess.run(["tasklist", "/FI", "IMAGENAME eq explorer.exe"],
                                capture_output=True, text=True)
        return "explorer.exe" in result.stdout.lower()
    except Exception:
        return True


def restart_explorer():
    try:
        log("Explorer not detected, restarting...")
        subprocess.run(["taskkill", "/F", "/IM", "explorer.exe"],
                       capture_output=True)
        time.sleep(1)
        subprocess.Popen(["explorer.exe"], cwd=os.environ.get("SystemRoot", r"C:\Windows"))
        time.sleep(2)
        log("Explorer restarted.")
    except Exception as e:
        log(f"Failed to restart Explorer: {e}")


def find_game_process():
    """Return PID of running Godot game process, or None."""
    try:
        result = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Godot_v4.6.2-stable_mono_win64.exe",
                                 "/FO", "CSV", "/NH"],
                                capture_output=True, text=True)
        lines = [l.strip() for l in result.stdout.strip().splitlines() if l.strip()]
        for line in lines:
            parts = line.split('","')
            if len(parts) >= 2 and "godot" in parts[0].lower():
                pid_str = parts[1].replace('"', '')
                return int(pid_str)
        return None
    except Exception:
        return None


def send_wm_close(pid):
    """Gently ask the process to close via WM_CLOSE using taskkill without /F."""
    try:
        log(f"Sending gentle close signal to PID {pid}...")
        result = subprocess.run(["taskkill", "/PID", str(pid)],
                                capture_output=True, text=True)
        log(f"taskkill result: {result.stdout.strip()} {result.stderr.strip()}")
        return True
    except Exception as e:
        log(f"Gentle close failed: {e}")
        return False


def copy_screenshots_from_godot():
    """Copy all zoom-level screenshots from Godot user:// to project directory."""
    os.makedirs(SCREENSHOT_DIR, exist_ok=True)
    copied = []
    missing = []

    for zoom in ZOOM_LEVELS:
        filename = f"auto_test_grid_zoom_{zoom}.png"
        # Try multiple possible locations
        candidates = [
            os.path.join(GODOT_USER_DIR, filename),
            os.path.join(PROJECT_PATH, "user", filename),
            os.path.join(PROJECT_PATH, ".godot", filename),
        ]
        found = False
        for src in candidates:
            if os.path.exists(src):
                dst = os.path.join(SCREENSHOT_DIR, filename)
                shutil.copy2(src, dst)
                copied.append(filename)
                found = True
                break
        if not found:
            missing.append(filename)

    return copied, missing


def main():
    log("Starting safe automated grid visibility test (multi-zoom)...")
    os.makedirs(SCREENSHOT_DIR, exist_ok=True)

    # 1. Kill any existing Godot game process first
    existing_pid = find_game_process()
    if existing_pid:
        log(f"Closing existing Godot process (PID {existing_pid})...")
        send_wm_close(existing_pid)
        time.sleep(3)
        existing_pid = find_game_process()
        if existing_pid:
            log("Existing process still alive after WM_CLOSE, waiting 3 more seconds...")
            time.sleep(3)

    # 2. Launch Godot in WINDOWED mode with fixed resolution
    log("Launching Godot in windowed mode (1280x720)...")
    proc = subprocess.Popen([
        GODOT_EXE,
        "--path", PROJECT_PATH,
        "--test-grid-visibility",
        "--windowed",
        "--resolution", "1280x720",
    ])

    # 3. Wait for Godot to finish on its own (it will screenshot and quit)
    log("Waiting for Godot to auto-screenshot and exit (timeout: 90s)...")
    max_wait = 90
    for i in range(max_wait):
        time.sleep(1)
        if proc.poll() is not None:
            log(f"Godot exited cleanly after {i + 1}s.")
            break
        if i % 5 == 0:
            if not is_explorer_running():
                restart_explorer()
    else:
        log("WARNING: Godot did not exit within 90s. Sending WM_CLOSE...")
        send_wm_close(proc.pid)
        time.sleep(3)
        if proc.poll() is None:
            log("WARNING: Godot still running. Sending WM_CLOSE again...")
            send_wm_close(proc.pid)
            time.sleep(5)
        if proc.poll() is None:
            log("ERROR: Godot refuses to close. Please close it manually.")
            return 1

    # 4. Copy screenshots
    time.sleep(1)
    copied, missing = copy_screenshots_from_godot()
    if copied:
        log(f"Copied {len(copied)} screenshots to {SCREENSHOT_DIR}")
        for f in copied:
            log(f"  - {f}")
    if missing:
        log(f"WARNING: {len(missing)} screenshots missing:")
        for f in missing:
            log(f"  - {f}")

    # 5. Final safety check
    if not is_explorer_running():
        restart_explorer()

    log("Test complete.")
    return 0 if not missing else 1


if __name__ == "__main__":
    sys.exit(main())
