: generate *.pyd
cythonize -i -3 _mp.py _sp.py chichi.py
: remove *.c
del _mp.c _sp.c chichi.c
: move *.pyd to temp
mkdir temp
move _mp.cp38-win32.pyd temp\_mp.pyd
move _sp.cp38-win32.pyd temp\_sp.pyd
move chichi.cp38-win32.pyd temp\chichi.pyd
: copy nessesary files
copy server.py temp
copy icon.ico temp
: build
cd temp
pyinstaller -F -i icon.ico ^
    --hidden-import libtorrent ^
    --hidden-import json ^
    --hidden-import rarfile ^
    --hidden-import psutil ^
    --hidden-import _sp ^
    --hidden-import requests_html ^
    --hidden-import _mp ^
    server.py
: finish
move /Y dist\server.exe ..\..\server.exe
cd ..
rmdir /S /Q temp
echo done
pause