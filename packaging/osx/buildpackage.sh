#!/bin/bash
# OpenRA packaging script for macOS
# Requires macOS host

command -v curl >/dev/null 2>&1 || { echo >&2 "macOS packaging requires curl."; exit 1; }
command -v markdown >/dev/null 2>&1 || { echo >&2 "macOS packaging requires markdown."; exit 1; }
command -v hdiutil >/dev/null 2>&1 || { echo >&2 "macOS packaging requires hdiutil."; exit 1; }

LAUNCHER_TAG="osx-launcher-20190317"

if [ $# -ne "2" ]; then
	echo "Usage: $(basename "$0") tag outputdir"
    exit 1
fi

# Set the working dir to the location of this script
cd "$(dirname "$0")" || exit 1

TAG="$1"
OUTPUTDIR="$2"
SRCDIR="$(pwd)/../.."
BUILTDIR="$(pwd)/build"

modify_plist() {
	sed "s|$1|$2|g" "$3" > "$3.tmp" && mv "$3.tmp" "$3"
}

# Copies the game files and sets metadata
populate_template() {
	TEMPLATE_DIR="${BUILTDIR}/${1}"
	MOD_ID=${2}
	MOD_NAME=${3}
	cp -r "${BUILTDIR}/OpenRA.app" "${TEMPLATE_DIR}"

	# Copy macOS specific files
	cp "${MOD_ID}.icns" "${TEMPLATE_DIR}/Contents/Resources/"
	modify_plist "{MOD_ID}" "${MOD_ID}" "${TEMPLATE_DIR}/Contents/Info.plist"
	modify_plist "{MOD_NAME}" "${MOD_NAME}" "${TEMPLATE_DIR}/Contents/Info.plist"
	modify_plist "{JOIN_SERVER_URL_SCHEME}" "openra-${MOD_ID}-${TAG}" "${TEMPLATE_DIR}/Contents/Info.plist"
}

# Deletes from the first argument's mod dirs all the later arguments
delete_mods() {
	pushd "${BUILTDIR}/${1}/Contents/Resources/mods" > /dev/null || exit 1
	shift
	rm -rf "$@"
	pushd > /dev/null || exit 1
}

echo "Building launchers"
curl -s -L -O https://github.com/OpenRA/OpenRALauncherOSX/releases/download/${LAUNCHER_TAG}/launcher.zip || exit 3
unzip -qq -d "${BUILTDIR}" launcher.zip
rm launcher.zip

# Background image is created from source svg in artsrc repository
# exported to tiff at 72 + 144 DPI, then combined using
# tiffutil -cathidpicheck bg.tiff bg2x.tiff -out background.tiff
cp background.tiff "/Volumes/OpenRA/.background.tiff"
cp ra.icns "/Volumes/OpenRA/.VolumeIcon.icns"

modify_plist "{DEV_VERSION}" "${TAG}" "${BUILTDIR}/OpenRA.app/Contents/Info.plist"
modify_plist "{FAQ_URL}" "http://wiki.openra.net/FAQ" "${BUILTDIR}/OpenRA.app/Contents/Info.plist"
echo "Building core files"

pushd "${SRCDIR}" > /dev/null || exit 1
make clean
make osx-dependencies
make core
make version VERSION="${TAG}"
make install-core gameinstalldir="/Contents/Resources/" DESTDIR="${BUILTDIR}/OpenRA.app"
popd > /dev/null || exit 1

curl -s -L -O https://raw.githubusercontent.com/wiki/OpenRA/OpenRA/Changelog.md
markdown Changelog.md > "${BUILTDIR}/OpenRA.app/Contents/Resources/CHANGELOG.html"
rm Changelog.md

markdown "${SRCDIR}/README.md" > "${BUILTDIR}/OpenRA.app/Contents/Resources/README.html"
markdown "${SRCDIR}/CONTRIBUTING.md" > "${BUILTDIR}/OpenRA.app/Contents/Resources/CONTRIBUTING.html"

populate_template "OpenRA - Red Alert.app" "ra" "Red Alert"
delete_mods "OpenRA - Red Alert.app" "cnc" "d2k"

populate_template "OpenRA - Tiberian Dawn.app" "cnc" "Tiberian Dawn"
delete_mods "OpenRA - Tiberian Dawn.app" "ra" "d2k"

populate_template "OpenRA - Dune 2000.app" "d2k" "Dune 2000"
delete_mods "OpenRA - Dune 2000.app" "ra" "cnc"

rm -rf "${BUILTDIR}/OpenRA.app"

echo "Packaging disk image"
hdiutil create build.dmg -format UDRW -volname "OpenRA" -fs HFS+ -srcfolder build
DMG_DEVICE=$(hdiutil attach -readwrite -noverify -noautoopen "build.dmg" | egrep '^/dev/' | sed 1q | awk '{print $1}')
sleep 2

echo '
   tell application "Finder"
     tell disk "'OpenRA'"
           open
           set current view of container window to icon view
           set toolbar visible of container window to false
           set statusbar visible of container window to false
           set the bounds of container window to {400, 100, 1040, 580}
           set theViewOptions to the icon view options of container window
           set arrangement of theViewOptions to not arranged
           set icon size of theViewOptions to 72
           set background picture of theViewOptions to file ".background.tiff"
           make new alias file at container window to POSIX file "/Applications" with properties {name:"Applications"}
           set position of item "'OpenRA - Tiberian Dawn.app'" of container window to {160, 176}
           set position of item "'OpenRA - Red Alert.app'" of container window to {320, 176}
           set position of item "'OpenRA - Dune 2000.app'" of container window to {480, 176}
           set position of item "Applications" of container window to {320, 368}
           set position of item ".background.tiff" of container window to {160, 368}
           set position of item ".fseventsd" of container window to {160, 368}
           set position of item ".VolumeIcon.icns" of container window to {160, 368}
           update without registering applications
           delay 5
           close
     end tell
   end tell
' | osascript

# HACK: Copy the volume icon again - something in the previous step seems to delete it...?
cp ra.icns "/Volumes/OpenRA/.VolumeIcon.icns"
SetFile -c icnC "/Volumes/OpenRA/.VolumeIcon.icns"
chmod -Rf go-w /Volumes/OpenRA
sync
sync
hdiutil detach ${DMG_DEVICE}
hdiutil convert build.dmg -format UDZO -imagekey zlib-level=9 -ov -o "${OUTPUTDIR}/OpenRA-${TAG}.dmg"
rm build.dmg

# Clean up
rm -rf libdmg-hfsplus-master "${BUILTDIR}"
