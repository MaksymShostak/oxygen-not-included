@echo off
:: Dynamically fetch the current user's true Documents folder path
for /f "usebackq tokens=*" %%i in (`powershell -NoProfile -Command "[Environment]::GetFolderPath('MyDocuments')"`) do set "USER_DOCS=%%i"

set "DEST_DIR=%USER_DOCS%\Klei\OxygenNotIncluded\mods\local\DeliveryTemperatureLimit"

:: Get the absolute path of the parent folder of this script
for %%I in ("%~dp0..") do set "PARENT_DIR=%%~fI"

:: Create the destination folder if it does not exist
if not exist "%DEST_DIR%" (
    echo Creating directory: %DEST_DIR%
    mkdir "%DEST_DIR%"
)

:: Copy and overwrite all .yaml files from the parent directory
copy /y "%PARENT_DIR%\*.yaml" "%DEST_DIR%\"

:: Copy and overwrite the specific .dll file from the parent directory
if exist "%PARENT_DIR%\DeliveryTemperatureLimit.dll" (
    copy /y "%PARENT_DIR%\DeliveryTemperatureLimit.dll" "%DEST_DIR%\"
) else (
    echo Warning: DeliveryTemperatureLimit.dll not found in the parent folder: %PARENT_DIR%
)

echo.
echo Deployment complete to: %DEST_DIR%
pause