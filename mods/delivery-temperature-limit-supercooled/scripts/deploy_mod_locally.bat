@echo off
:: Dynamically fetch the current user's true Documents folder path
for /f "usebackq tokens=*" %%i in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')"`) do set "USER_DOCS=%%i"

set "DEST_DIR=%USER_DOCS%\Klei\OxygenNotIncluded\mods\local\DeliveryTemperatureLimit"

:: Get the absolute path of the parent folder of this script
for %%I in ("%~dp0..") do set "PARENT_DIR=%%~fI"

echo ===================================================
echo   Deploying Mod Locally to Oxygen Not Included
echo ===================================================
echo Source:      %PARENT_DIR%
echo Destination: %DEST_DIR%
echo.

:: Create the destination folder if it does not exist
if not exist "%DEST_DIR%" (
    echo [INFO] Target mod directory does not exist. Creating it now...
    mkdir "%DEST_DIR%"
)

:: Copy and overwrite all .yaml files from the parent directory
echo [1/3] Copying mod metadata yaml files...
copy /y "%PARENT_DIR%\*.yaml" "%DEST_DIR%\" >nul

:: Copy and overwrite the preview image if it exists
if exist "%PARENT_DIR%\Preview.png" (
    echo [2/3] Copying Preview.png...
    copy /y "%PARENT_DIR%\Preview.png" "%DEST_DIR%\" >nul
) else if exist "%PARENT_DIR%\preview.png" (
    echo [2/3] Copying preview.png...
    copy /y "%PARENT_DIR%\preview.png" "%DEST_DIR%\" >nul
) else (
    echo [WARN] No Preview.png found! Mod will deploy without thumbnail.
)

:: Copy and overwrite the specific .dll file from the parent directory
if exist "%PARENT_DIR%\DeliveryTemperatureLimit.dll" (
    echo [3/3] Copying DeliveryTemperatureLimit.dll...
    copy /y "%PARENT_DIR%\DeliveryTemperatureLimit.dll" "%DEST_DIR%\" >nul
    echo.
    echo [SUCCESS] Mod deployed successfully! Ready to test in-game.
) else (
    echo.
    echo [ERROR] DeliveryTemperatureLimit.dll NOT found in: %PARENT_DIR%
    echo Please compile the project in Release mode before deploying.
)

echo ===================================================
echo.
pause