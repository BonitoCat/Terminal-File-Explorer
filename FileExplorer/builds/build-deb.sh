#! /bin/bash
bash -c "dpkg-deb --build fe-linux-x64"
bash -c "ARCH=x86_64 ./appimagetool-x86_64.AppImage AppDir"
