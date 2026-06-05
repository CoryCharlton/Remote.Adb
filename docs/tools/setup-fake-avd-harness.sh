#!/usr/bin/env bash
#
# Builds a deterministic, throwaway Android SDK + AVD harness so the app exercises the full
# list / view / edit / CREATE flow WITHOUT a real Android SDK. Used for WSLg screenshot-driven UI
# debugging and console smoke tests — see docs/wslg-gui-debugging.md.
#
# Creates a structured fake SDK root (so IAndroidSdk resolves every tool + the system-images scan):
#   $SDK/emulator/emulator                       — lists AVDs by scanning ANDROID_AVD_HOME
#   $SDK/platform-tools/adb                       — reports no attached devices
#   $SDK/cmdline-tools/latest/bin/avdmanager      — list device / create avd / delete avd (writes $AVDS)
#   $SDK/cmdline-tools/latest/bin/sdkmanager      — stub
#   $SDK/system-images/android-*/<tag>/<abi>/     — installed images the create wizard lists
#   $AVDS/<name>.avd/config.ini                   — seeded AVDs (1 rich TV + 14 minimal Pixels)
#
# Run the app/console against it with ANDROID_HOME (the SDK root) + ANDROID_AVD_HOME:
#   ANDROID_HOME=$SDK ANDROID_AVD_HOME=$AVDS DISPLAY=:0 \
#     dotnet src/Remote.Adb.Desktop/bin/Release/net10.0/Remote.Adb.Desktop.dll
set -euo pipefail

SDK="${1:-/tmp/fakesdk}"
AVDS="${2:-/tmp/avdhome}"

rm -rf "$SDK" "$AVDS"
mkdir -p "$SDK/emulator" "$SDK/platform-tools" "$SDK/cmdline-tools/latest/bin" "$AVDS"

# --- installed system images the create wizard enumerates (system-images/android-<n>/<tag>/<abi>/) ---
for image in \
    "android-34/google_apis_playstore/x86_64" \
    "android-34/google_apis/x86_64" \
    "android-36/google_apis_tv/x86_64" \
    "android-33/google_apis/arm64-v8a"; do
  mkdir -p "$SDK/system-images/$image"
done

# --- fake `emulator`: -list-avds reflects whatever is in ANDROID_AVD_HOME (so create/delete show up) ---
cat > "$SDK/emulator/emulator" <<'EOF'
#!/bin/sh
if [ "$1" = "-list-avds" ]; then
  home="${ANDROID_AVD_HOME:-$HOME/.android/avd}"
  for dir in "$home"/*.avd; do
    [ -d "$dir" ] || continue
    basename "$dir" .avd
  done
fi
exit 0
EOF

# --- fake `adb`: no attached devices ---
cat > "$SDK/platform-tools/adb" <<'EOF'
#!/bin/sh
if [ "$1" = "devices" ]; then
  echo "List of devices attached"
  echo ""
fi
exit 0
EOF

# --- fake `avdmanager`: list device / create avd / delete avd ---
cat > "$SDK/cmdline-tools/latest/bin/avdmanager" <<'EOF'
#!/bin/sh
home="${ANDROID_AVD_HOME:-$HOME/.android/avd}"
cmd="$1"; sub="$2"; shift 2 2>/dev/null || true

name=""; pkg=""; device=""
while [ $# -gt 0 ]; do
  case "$1" in
    -n) name="$2"; shift 2 ;;
    -k) pkg="$2"; shift 2 ;;
    -d) device="$2"; shift 2 ;;
    *) shift ;;
  esac
done

if [ "$cmd" = "list" ] && [ "$sub" = "device" ]; then
  cat <<DEV
Available devices definitions:
id: 0 or "tv_1080p"
    Name: Television (1080p)
    OEM : Google
---------
id: 9 or "pixel_6"
    Name: Pixel 6
    OEM : Google
---------
id: 17 or "Nexus 5"
    Name: Nexus 5
    OEM : Google
---------
DEV
  exit 0
fi

if [ "$cmd" = "create" ] && [ "$sub" = "avd" ]; then
  cat >/dev/null 2>&1 || true   # swallow the piped "no" prompt answer
  [ -n "$name" ] || { echo "Error: missing -n" >&2; exit 1; }
  dir="$home/$name.avd"
  mkdir -p "$dir"
  sysdir="$(printf '%s' "$pkg" | tr ';' '/')/"
  tag="$(printf '%s' "$pkg" | cut -d';' -f3)"
  abi="$(printf '%s' "$pkg" | cut -d';' -f4)"
  {
    echo "AvdId=$name"
    echo "avd.ini.displayname=$name"
    echo "tag.displaynames=$tag"
    echo "image.sysdir.1=$sysdir"
    echo "abi.type=$abi"
    echo "hw.device.name=$device"
  } > "$dir/config.ini"
  echo "Created AVD '$name'"
  exit 0
fi

if [ "$cmd" = "delete" ] && [ "$sub" = "avd" ]; then
  rm -rf "$home/$name.avd"
  echo "Deleted AVD '$name'"
  exit 0
fi

exit 0
EOF

# --- fake `sdkmanager`: stub (installed images come from the filesystem scan above) ---
cat > "$SDK/cmdline-tools/latest/bin/sdkmanager" <<'EOF'
#!/bin/sh
exit 0
EOF

chmod +x \
  "$SDK/emulator/emulator" \
  "$SDK/platform-tools/adb" \
  "$SDK/cmdline-tools/latest/bin/avdmanager" \
  "$SDK/cmdline-tools/latest/bin/sdkmanager"

# --- minimal Pixel AVDs (exercise the short list path + a few detail groups) ---
for i in $(seq 1 14); do
  dir="$AVDS/Pixel_$i.avd"
  mkdir -p "$dir"
  cat > "$dir/config.ini" <<EOF
AvdId=Pixel_$i
avd.ini.displayname=Pixel $i
tag.displaynames=Google Play
image.sysdir.1=system-images/android-34/google_apis/x86_64/
hw.ramSize=2048
EOF
done

# --- one rich AVD: every detail group populated, for scroll/overflow testing ---
tv="$AVDS/Television_1080p_16.0.avd"
mkdir -p "$tv"
cat > "$tv/config.ini" <<EOF
AvdId=Television_1080p_16.0
avd.ini.displayname=Television (1080p) 16.0
tag.id=google_apis_tv
tag.displaynames=Google TV
image.sysdir.1=system-images/android-36/google_apis_tv/x86_64/
abi.type=x86_64
hw.cpu.arch=x86_64
hw.device.name=tv_1080p
hw.device.manufacturer=Google
hw.lcd.width=1920
hw.lcd.height=1080
hw.lcd.density=320
hw.ramSize=2048
vm.heapSize=256
disk.dataPartition.size=6442450944
hw.sdCard=yes
sdcard.size=512M
hw.cpu.ncore=4
hw.gpu.enabled=yes
hw.gpu.mode=auto
hw.camera.front=emulated
hw.camera.back=none
hw.gps=yes
hw.keyboard=yes
hw.initialOrientation=landscape
hw.audioInput=yes
skin.name=tv_1080p
skin.path=tv_1080p
showDeviceFrame=yes
runtime.network.speed=full
runtime.network.latency=none
EOF
# sibling <AvdId>.ini drives the Location group (path=/target=)
cat > "$AVDS/Television_1080p_16.0.ini" <<EOF
path=$tv
target=android-36
EOF

echo "Harness ready:"
echo "  SDK (ANDROID_HOME)     : $SDK   (emulator, adb, avdmanager, sdkmanager, system-images)"
echo "  AVDs (ANDROID_AVD_HOME): $AVDS  (15 AVDs)"
