@echo off
setlocal

set "ROOT=%~dp0"
set "COMMAND=%~1"
set "CLIENT_DLL=%ROOT%src\AdrienCoder.Client.Cli\bin\Release\net10.0\AdrienCoder.Client.Cli.dll"
set "SERVER_DLL=%ROOT%src\AdrienCoder.Server\bin\Release\net10.0\AdrienCoder.Server.dll"
set "WORKER_DLL=%ROOT%src\AdrienCoder.WorkerGpu\bin\Release\net10.0\AdrienCoder.WorkerGpu.dll"
set "CLIENT_DIR=%ROOT%src\AdrienCoder.Client.Cli\bin\Release\net10.0"
set "SERVER_DIR=%ROOT%src\AdrienCoder.Server\bin\Release\net10.0"
set "WORKER_DIR=%ROOT%src\AdrienCoder.WorkerGpu\bin\Release\net10.0"

if "%COMMAND%"=="" goto help
if /I "%COMMAND%"=="help" goto help
if /I "%COMMAND%"=="--help" goto help
if /I "%COMMAND%"=="-h" goto help
if /I "%COMMAND%"=="local" goto local
if /I "%COMMAND%"=="vps" goto vps

:route
if /I "%COMMAND%"=="index" goto default_client
if /I "%COMMAND%"=="chat" goto default_client
if /I "%COMMAND%"=="ask" goto default_client
if /I "%COMMAND%"=="status" goto default_client
if /I "%COMMAND%"=="models" goto default_client
if /I "%COMMAND%"=="server" goto server
if /I "%COMMAND%"=="worker" goto worker
if /I "%COMMAND%"=="build" goto build

echo Commande inconnue: %COMMAND%
echo.
goto help_error

:local
goto client

:vps
goto client

:default_client
set "Server__BaseUrl=https://adrien-sheng-lin.fr/adriencoder/"
goto client

:client
if not exist "%CLIENT_DLL%" (
  echo Le Client CLI n'est pas compile. Lancez: adriencoder build
  exit /b 1
)
pushd "%CLIENT_DIR%"
dotnet "%CLIENT_DLL%" %*
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%

:server
if not exist "%SERVER_DLL%" (
  echo Le Server n'est pas compile. Lancez: adriencoder build
  exit /b 1
)
set "ASPNETCORE_ENVIRONMENT=Local"
if not defined Embedding__ApiFormat set "Embedding__ApiFormat=Ollama"
if not defined Embedding__BaseUrl set "Embedding__BaseUrl=http://localhost:11434"
if not defined Embedding__ApiKey set "Embedding__ApiKey="
if not defined Embedding__Model set "Embedding__Model=nomic-embed-text"
if not defined Embedding__VectorSize set "Embedding__VectorSize=768"
pushd "%SERVER_DIR%"
dotnet "%SERVER_DLL%"
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%

:worker
if not exist "%WORKER_DLL%" (
  echo Le WorkerGpu n'est pas compile. Lancez: adriencoder build
  exit /b 1
)
pushd "%WORKER_DIR%"
dotnet "%WORKER_DLL%"
set "EXIT_CODE=%ERRORLEVEL%"
popd
exit /b %EXIT_CODE%

:build
dotnet build "%ROOT%AdrienCoder.sln" -c Release
exit /b %ERRORLEVEL%

:help
echo AdrienCoder
echo.
echo   adriencoder build
echo   adriencoder server
echo   adriencoder worker
echo   adriencoder index ^<repoPath^> [repositoryName]
echo   adriencoder chat [--repo repositoryName] [--no-context] ^<question...^>
echo   adriencoder ask ^<question...^>
echo   adriencoder status
echo   adriencoder models
echo   adriencoder local ^<index^|chat^> ...
echo.
echo Exemples:
echo   adriencoder index . AdrienCoder
echo   adriencoder chat --repo AdrienCoder "Explique l'architecture"
echo   adriencoder ask "Reponds juste ok"
echo   adriencoder status
echo   adriencoder local index . AdrienCoder
echo   adriencoder local chat --repo AdrienCoder "Explique l'architecture"
exit /b 0

:help_error
call :help
exit /b 1
