@echo off
chcp 65001 >nul
title SUBSISTENCE - sbor Windows-bilda
setlocal enabledelayedexpansion

echo.
echo  ==========================================================
echo   SUBSISTENCE  -  sborka Windows-bilda (odnim klikom)
echo   Unity 2022.3.62f2 LTS  /  HDRP 14.0.12  /  Mirror (opt.)
echo  ==========================================================
echo.

set "ROOT=%~dp0"
set "UNITY=%~1"

rem --- где лежит проект: рядом UnityProject\ или сам корень (если распаковал иначе) ---
set "PROJECT="
if exist "%ROOT%UnityProject\ProjectSettings\ProjectVersion.txt" set "PROJECT=%ROOT%UnityProject\"
if not defined PROJECT if exist "%ROOT%ProjectSettings\ProjectVersion.txt" set "PROJECT=%ROOT%"
if not defined PROJECT for /d %%D in ("%ROOT%*") do (
  if not defined PROJECT if exist "%%D\ProjectSettings\ProjectVersion.txt" set "PROJECT=%%D\"
)
if not defined PROJECT (
  echo  [НЕТ ПРОЕКТА] Рядом с этим .bat нет папки Unity-проекта ^(Assets + Packages + ProjectSettings^).
  echo                Положи .bat рядом с папкой UnityProject и запусти снова.
  pause
  exit /b 1
)

if not defined UNITY (
  for %%P in (
    "%ProgramFiles%\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
    "%ProgramW6432%\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
    "%ProgramFiles%\Unity Hub\Editor\2022.3.62f2\Editor\Unity.exe"
    "%ProgramFiles%\Unity\Editor\2022.3.62f2\Editor\Unity.exe"
    "C:\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
    "C:\Unity\2022.3.62f2\Editor\Unity.exe"
    "D:\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
    "D:\Unity\2022.3.62f2\Editor\Unity.exe"
    "D:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
    "E:\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe"
    "E:\Unity\2022.3.62f2\Editor\Unity.exe"
  ) do (
    if not defined UNITY if exist %%P set "UNITY=%%~P"
  )
)

if not defined UNITY (
  echo  [НЕТ UNITY] Не найден Unity 2022.3.62f2 на этом ПК.
  echo.
  echo   Вариант 1: перетащи мышкой файл Unity.exe на этот .bat
  echo   Вариант 2: запусти из консоли:  BUILD_WINDOWS.bat "D:\путь\к\Unity.exe"
  echo   Где взять: Unity Hub ^-^> Installs ^-^> 2022.3.62f2 LTS  ^(версия строго такая^)
  echo.
  pause
  exit /b 1
)

echo  Unity:   "!UNITY!"
echo  Проект:  "!PROJECT!"
echo.
rem --- режим: перетащи .bat с ключом play (или запусти: BUILD_WINDOWS.bat play) ---
set "MODE=build"
if /I "%~1"=="play" set "MODE=play"
if /I "%~2"=="play" set "MODE=play"

if "%MODE%"=="play" (
  echo  Режим: ОТКРЫТЬ РЕДАКТОР И ИГРАТЬ ^(без exe, быстрее^)
  echo  1^) откроется Unity, 2^) меню Subsistence ^-^> 10. Играть сейчас
  echo.
  start "" "!UNITY!" -projectPath "!PROJECT!"
  echo  Unity запущен. Если он спросит лицензию — войди в аккаунт Unity в Hub.
  timeout /t 5 >nul
  exit /b 0
)

echo  Идёт сборка: 5-20 минут. Первый запуск дольше (Unity качает пакеты HDRP).
echo  Окно не закрывай. Полный лог пишется в build_log.txt
echo.

"!UNITY!" -batchmode -quit -accept-apiupdate -projectPath "!PROJECT!" -buildTarget StandaloneWindows64 -logFile "!ROOT!build_log.txt" -executeMethod Subsistence.EditorTools.ProjectBootstrap.BuildAll

set "CODE=%ERRORLEVEL%"
echo.

if exist "!PROJECT!\Builds\Windows\Subsistence.exe" (
  echo  [ГОТОВО] Билд собран:
  echo           "!PROJECT!\Builds\Windows\Subsistence.exe"
  echo.
  echo  Лог: "!ROOT!build_log.txt"
  start "" "!PROJECT!\Builds\Windows"
) else (
  echo  [ОШИБКА] exe не появился. Код выхода Unity: %CODE%
  echo.
  rem --- самая частая причина: Unity не активирован (нет лицензии) ---
  findstr /I /C:"No valid Unity Editor license" /C:"Licensing" /C:"license is not" /C:"Entitlement-based" "!ROOT!build_log.txt" >nul 2>&1
  if !ERRORLEVEL! EQU 0 (
    echo  === ЭТО ЛИЦЕНЗИЯ ===
    echo  Unity не активирован, поэтому exe собрать нельзя. Как починить:
    echo    1^) открой Unity Hub ^-^> Sign in ^(бесплатный Personal подойдёт^)
    echo    2^) запусти этот .bat так:   BUILD_WINDOWS.bat play
    echo       ^(откроется редактор; меню Subsistence ^-^> 10. Играть сейчас^)
    echo    3^) либо: открой проект в Hub один раз ^(активация пройдёт^) и запусти .bat снова
    echo.
  )
  echo  Что делать:
  echo  Что делать:
  echo    1) открой build_log.txt и поищи "error CS" (ошибка компиляции) или "[Subsistence]"
  echo    2) если ошибок C# нет - открой проект в Unity и прогони меню:
  echo       Subsistence -^> 1, 2, 5, 7, 6  и затем "Собрать Windows-билд"
  if exist "!ROOT!build_log.txt" (
    echo.
    echo  --- последние 25 строк лога ---
    powershell -NoProfile -Command "Get-Content -Tail 25 '!ROOT!build_log.txt'" 2>nul
  )
  pause
  exit /b 1
)
pause
