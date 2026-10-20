#!/bin/bash

cd "$(dirname "$0")/.."

VERSION="1.0.0"

echo "Compiling macOS Intel (osx-x64) build for version $VERSION..."

rm -rf "Darksiders II Reborn.app" ./temp_backend ./temp_launcher

dotnet publish Darksiders2Reborn.csproj -c Release -r osx-x64 --self-contained true -p:UseAppHost=true -p:Version=$VERSION -o ./temp_backend
dotnet publish Darksiders2Reborn.Launcher/Darksiders2Reborn.Launcher.csproj -c Release -r osx-x64 --self-contained true -p:UseAppHost=true -p:Version=$VERSION -o ./temp_launcher

mkdir -p "Darksiders II Reborn.app/Contents/MacOS"
mkdir -p "Darksiders II Reborn.app/Contents/Backend"
mkdir -p "Darksiders II Reborn.app/Contents/Resources"

cp -R ./temp_launcher/* "Darksiders II Reborn.app/Contents/MacOS/"
cp -R ./temp_backend/* "Darksiders II Reborn.app/Contents/Backend/"
cp darksiders2.pfx "Darksiders II Reborn.app/Contents/Backend/"
cp darksiders2_rpcs3.pfx "Darksiders II Reborn.app/Contents/Backend/"
cp Assets/AppIcon.icns "Darksiders II Reborn.app/Contents/Resources/"

chmod +x "Darksiders II Reborn.app/Contents/MacOS/Darksiders2Reborn.Launcher"
chmod +x "Darksiders II Reborn.app/Contents/Backend/Darksiders2Reborn"

cat > "Darksiders II Reborn.app/Contents/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleExecutable</key>
    <string>Darksiders2Reborn.Launcher</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>CFBundleName</key>
    <string>Darksiders II Reborn</string>
    <key>CFBundleDisplayName</key>
    <string>Darksiders II Reborn</string>
    <key>CFBundleIdentifier</key>
    <string>com.adamstark.darksiders2reborn</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>$VERSION</string>
    <key>CFBundleVersion</key>
    <string>$VERSION</string>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
</dict>
</plist>
EOF

rm -rf ./temp_backend ./temp_launcher
xattr -cr "Darksiders II Reborn.app"

codesign --force --sign - "Darksiders II Reborn.app/Contents/Backend/Darksiders2Reborn"
codesign --force --sign - "Darksiders II Reborn.app/Contents/MacOS/Darksiders2Reborn.Launcher"
codesign --force --deep --sign - "Darksiders II Reborn.app"

xattr -cr "Darksiders II Reborn.app"
touch "Darksiders II Reborn.app"

echo "MacOS Intel build compiled. Happy trophy hunting! :)"