$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    . ./scripts/dev-env.ps1
    $env:CAREERGAP_TEST_DATABASE = $env:ConnectionStrings__Database
    dotnet test tests/CareerGap.Api.Tests/CareerGap.Api.Tests.csproj --logger 'trx;LogFileName=foundation.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Backend tests failed' }
    Push-Location frontend
    try {
        npm run lint; if ($LASTEXITCODE -ne 0) { throw 'Lint failed' }
        npm run typecheck; if ($LASTEXITCODE -ne 0) { throw 'Typecheck failed' }
        npm test; if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed' }
        npm run build; if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed' }
    } finally { Pop-Location }
} finally { Pop-Location }
